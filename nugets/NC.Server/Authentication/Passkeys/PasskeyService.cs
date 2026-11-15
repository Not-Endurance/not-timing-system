using System.Buffers.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using NC.Server.Authentication.Email;
using NC.Server.Authentication.Identity;

namespace NC.Server.Authentication.Passkeys;

/// <summary>
/// Passkeys on top of ASP.NET Core Identity's passkey handler: the options for creating and using one, the check of the
/// browser's answer, and the management of the passkeys a user has. Every change goes through a concurrency-checked update.
/// </summary>
internal sealed class PasskeyService : IPasskeyManager
{
    /// <summary>A user keeps at least one passkey, so the last one is never removed.</summary>
    const int MinimumPasskeys = 1;

    readonly IPasskeyCeremony _ceremony;
    readonly IIdentityUserRepository _users;
    readonly EmailDispatchQueue _mail;
    readonly EmailLanguage _languages;
    readonly TimeProvider _time;
    readonly ILogger<PasskeyService> _logger;

    public PasskeyService(
        IPasskeyCeremony ceremony,
        IIdentityUserRepository users,
        EmailDispatchQueue mail,
        EmailLanguage languages,
        TimeProvider time,
        ILogger<PasskeyService> logger
    )
    {
        _ceremony = ceremony;
        _users = users;
        _mail = mail;
        _languages = languages;
        _time = time;
        _logger = logger;
    }

    /// <summary>Options for a passkey sign-in. They name no user, so the authenticator offers the passkeys it holds.</summary>
    public Task<string> SignInOptions()
    {
        return _ceremony.MakeRequestOptions();
    }

    /// <summary>
    /// Options for a new passkey. The user gets a random handle the first time, never the integer id. Null when the
    /// handle could not be stored, so the caller can ask again.
    /// </summary>
    public async Task<string?> CreationOptions(int userId, CancellationToken cancellationToken)
    {
        var user =
            await _users.FindByUserId(userId, cancellationToken)
            ?? throw new InvalidOperationException("The signed-in user does not exist.");

        if (user.UserHandle is null)
        {
            await _users.Change(
                userId,
                changed =>
                {
                    changed.UserHandle ??= System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
                    return _users.UpdateAsync(changed, cancellationToken);
                },
                cancellationToken
            );
            user = await _users.FindByUserId(userId, cancellationToken) ?? user;
            if (user.UserHandle is null)
            {
                return null;
            }
        }

        return await _ceremony.MakeCreationOptions(
            new PasskeyUserEntity
            {
                Id = Base64Url.EncodeToString(user.UserHandle),
                Name = user.Email ?? string.Empty,
                DisplayName = user.DisplayName ?? user.Name ?? user.Email ?? string.Empty,
            }
        );
    }

    /// <summary>
    /// Stores the passkey the browser created for the signed-in user, and tells them by email. The request sets the
    /// language of the email.
    /// </summary>
    public async Task<PasskeyOutcome> Enrol(
        HttpContext httpContext,
        int userId,
        string credentialJson,
        string? name,
        CancellationToken cancellationToken
    )
    {
        var passkey = await TryCeremony(() => _ceremony.PerformAttestation(credentialJson));
        if (passkey is null)
        {
            return new PasskeyOutcome { Kind = PasskeyOutcomeKind.Refused };
        }

        var now = _time.GetUtcNow();
        passkey.Name = string.IsNullOrWhiteSpace(name) ? $"Passkey, {now:yyyy-MM-dd}" : name.Trim();

        var result = await _users.Change(
            userId,
            async changed =>
            {
                await _users.AddOrUpdatePasskeyAsync(changed, passkey, cancellationToken);
                return await _users.UpdateAsync(changed, cancellationToken);
            },
            cancellationToken
        );
        if (!result.Succeeded)
        {
            return new PasskeyOutcome { Kind = PasskeyOutcomeKind.Refused };
        }

        var user = await _users.FindByUserId(userId, cancellationToken);
        if (user?.Email != null)
        {
            var language = _languages.Choose(httpContext.Request.Headers.AcceptLanguage.ToString());
            _mail.Enqueue(EmailTemplates.PasskeyAdded(user.Email, language, passkey.Name!, now));
        }

        _logger.LogInformation("User {UserId} added a passkey.", userId);
        return new PasskeyOutcome { Kind = PasskeyOutcomeKind.Done, Name = passkey.Name };
    }

    /// <summary>
    /// Checks a passkey sign-in and returns its user, with the passkey's sign count recorded. Null when the answer is not
    /// accepted, or when the user has no security stamp: a session is bound to the stamp.
    /// </summary>
    public async Task<IdentityUserDocument?> VerifySignIn(string credentialJson, CancellationToken cancellationToken)
    {
        var assertion = await TryCeremony(() => _ceremony.PerformAssertion(credentialJson));
        if (assertion is null)
        {
            return null;
        }

        var userId = assertion.User.Id;
        var passkey = assertion.Passkey;
        await _users.Change(
            userId,
            async changed =>
            {
                await _users.AddOrUpdatePasskeyAsync(changed, passkey, cancellationToken);
                return await _users.UpdateAsync(changed, cancellationToken);
            },
            cancellationToken
        );

        var user = await _users.FindByUserId(userId, cancellationToken) ?? assertion.User;
        if (user.SecurityStamp is null)
        {
            _logger.LogWarning("A passkey sign-in was refused: the user has no security stamp.");
            return null;
        }

        return user;
    }

    public async Task<IReadOnlyList<PasskeyInfo>> List(int userId, CancellationToken cancellationToken)
    {
        var user = await _users.FindByUserId(userId, cancellationToken);
        if (user is null)
        {
            return [];
        }

        return
        [
            .. (await _users.GetPasskeysAsync(user, cancellationToken))
                .OrderBy(x => x.CreatedAt)
                .Select(x => new PasskeyInfo
                {
                    CredentialId = x.CredentialId,
                    Name = x.Name,
                    CreatedAt = x.CreatedAt,
                }),
        ];
    }

    /// <summary>Renames one of the user's passkeys. The name is what the user sees on the Passkeys page to tell them apart.</summary>
    public async Task<PasskeyOutcome> Rename(
        int userId,
        byte[] credentialId,
        string name,
        CancellationToken cancellationToken
    )
    {
        var trimmed = name.Trim();
        var renamed = await _users.RenamePasskey(userId, credentialId, trimmed, cancellationToken);
        return renamed
            ? new PasskeyOutcome { Kind = PasskeyOutcomeKind.Done, Name = trimmed }
            : new PasskeyOutcome { Kind = PasskeyOutcomeKind.NotFound };
    }

    /// <summary>Removes a passkey, but never the user's last one. The repository keeps that minimum in the same update.</summary>
    public async Task<PasskeyOutcome> Remove(int userId, byte[] credentialId, CancellationToken cancellationToken)
    {
        if (await _users.RemovePasskeyKeepingAtLeast(userId, credentialId, MinimumPasskeys, cancellationToken))
        {
            return new PasskeyOutcome { Kind = PasskeyOutcomeKind.Done };
        }

        // Refused either because the passkey is not the user's or because it is their last. Reading again tells them apart.
        var user = await _users.FindByUserId(userId, cancellationToken);
        var stillHeld = user?.Passkeys?.Any(x => x.CredentialId.AsSpan().SequenceEqual(credentialId)) ?? false;
        return new PasskeyOutcome { Kind = stillHeld ? PasskeyOutcomeKind.LastPasskey : PasskeyOutcomeKind.NotFound };
    }

    /// <summary>An answer that cannot be checked is refused the same way whatever the reason. Cancellation is not.</summary>
    async Task<T?> TryCeremony<T>(Func<Task<T?>> ceremony)
        where T : class
    {
        try
        {
            return await ceremony();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogInformation("A passkey ceremony was refused: {FailureKind}.", ex.GetType().Name);
            return null;
        }
    }
}

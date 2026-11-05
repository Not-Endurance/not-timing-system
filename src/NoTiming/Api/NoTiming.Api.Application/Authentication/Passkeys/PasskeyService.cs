using System.Buffers.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NoTiming.Api.Application.Authentication.Email;
using NoTiming.Api.Application.Authentication.Sessions;
using NoTiming.Api.Application.Authentication.Store;

namespace NoTiming.Api.Application.Authentication.Passkeys;

public enum PasskeyOutcomeKind
{
    Done,
    NotFound,
    LastPasskey,
    Refused,
}

public sealed class PasskeyOutcome
{
    public PasskeyOutcomeKind Kind { get; init; }

    public string? Name { get; init; }
}

/// <summary>
/// Passkeys on top of ASP.NET Core Identity's passkey handler: the options for creating and using one, the check of the
/// browser's answer, and the management of the passkeys a user has. Every change goes through a concurrency-checked update.
/// </summary>
public sealed class PasskeyService
{
    readonly SignInManager<IdentityUserDocument> _signIn;
    readonly IdentityUserStore _users;
    readonly SessionStore _sessions;
    readonly EmailDispatchQueue _mail;
    readonly TimeProvider _time;
    readonly ILogger<PasskeyService> _logger;

    public PasskeyService(
        SignInManager<IdentityUserDocument> signIn,
        IdentityUserStore users,
        SessionStore sessions,
        EmailDispatchQueue mail,
        TimeProvider time,
        ILogger<PasskeyService> logger
    )
    {
        _signIn = signIn;
        _users = users;
        _sessions = sessions;
        _mail = mail;
        _time = time;
        _logger = logger;
    }

    /// <summary>Options for a passkey sign-in. They name no user, so the authenticator offers the passkeys it holds.</summary>
    public Task<string> RequestOptionsAsync()
    {
        return _signIn.MakePasskeyRequestOptionsAsync(null!);
    }

    /// <summary>Options for a new passkey. The user gets a random handle the first time, never the integer id.</summary>
    public async Task<string> CreationOptionsAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await _users.FindByUserIdAsync(userId, cancellationToken)
            ?? throw new InvalidOperationException("The signed-in user does not exist.");

        if (user.UserHandle is null)
        {
            await _users.ChangeAsync(
                userId,
                changed =>
                {
                    changed.UserHandle ??= System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
                    return _users.UpdateAsync(changed, cancellationToken);
                },
                cancellationToken
            );
            user = await _users.FindByUserIdAsync(userId, cancellationToken) ?? user;
        }

        return await _signIn.MakePasskeyCreationOptionsAsync(
            new PasskeyUserEntity
            {
                Id = Base64Url.EncodeToString(user.UserHandle!),
                Name = user.Email ?? string.Empty,
                DisplayName = user.DisplayName ?? user.Name ?? user.Email ?? string.Empty,
            }
        );
    }

    /// <summary>Stores the passkey the browser created for the signed-in user, and tells them by email.</summary>
    public async Task<PasskeyOutcome> EnrolAsync(
        int userId,
        string credentialJson,
        string? name,
        string language,
        CancellationToken cancellationToken
    )
    {
        var attestation = await TryCeremonyAsync(() => _signIn.PerformPasskeyAttestationAsync(credentialJson));
        if (attestation?.Passkey is null)
        {
            return new PasskeyOutcome { Kind = PasskeyOutcomeKind.Refused };
        }

        var now = _time.GetUtcNow();
        var passkey = attestation.Passkey;
        passkey.Name = string.IsNullOrWhiteSpace(name) ? $"Passkey, {now:yyyy-MM-dd}" : name.Trim();

        var result = await _users.ChangeAsync(
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

        var user = await _users.FindByUserIdAsync(userId, cancellationToken);
        if (user?.Email != null)
        {
            _mail.Enqueue(Email.EmailTemplates.PasskeyAdded(user.Email, language, passkey.Name!, now));
        }

        _logger.LogInformation("User {UserId} added a passkey.", userId);
        return new PasskeyOutcome { Kind = PasskeyOutcomeKind.Done, Name = passkey.Name };
    }

    /// <summary>Checks a passkey sign-in and starts a session for its user. Null when the answer is not accepted.</summary>
    public async Task<PasskeySignIn?> SignInAsync(string credentialJson, CancellationToken cancellationToken)
    {
        var assertion = await TryCeremonyAsync(() => _signIn.PerformPasskeyAssertionAsync(credentialJson));
        if (assertion?.User is null || assertion.Passkey is null)
        {
            return null;
        }

        var userId = assertion.User.Id;
        var passkey = assertion.Passkey;
        await _users.ChangeAsync(
            userId,
            async changed =>
            {
                await _users.AddOrUpdatePasskeyAsync(changed, passkey, cancellationToken);
                return await _users.UpdateAsync(changed, cancellationToken);
            },
            cancellationToken
        );

        var user = await _users.FindByUserIdAsync(userId, cancellationToken) ?? assertion.User;
        var now = _time.GetUtcNow();
        var issued = SessionToken.Issue();
        await _sessions.CreateAsync(issued.TokenHash, user.Id, user.SecurityStamp!, now, cancellationToken);
        _logger.LogInformation("User {UserId} signed in with a passkey.", user.Id);

        return new PasskeySignIn { UserId = user.Id, SessionToken = issued.Token, ExpiresAtUtc = SessionRules.ExpiresAt(now) };
    }

    public async Task<IReadOnlyList<UserPasskeyInfo>> ListAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await _users.FindByUserIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return [];
        }

        return [.. (await _users.GetPasskeysAsync(user, cancellationToken)).OrderBy(x => x.CreatedAt)];
    }

    public async Task<PasskeyOutcome> RenameAsync(int userId, byte[] credentialId, string name, CancellationToken cancellationToken)
    {
        var found = false;
        var result = await _users.ChangeAsync(
            userId,
            changed =>
            {
                var passkey = changed.Passkeys?.Find(x => x.CredentialId.AsSpan().SequenceEqual(credentialId));
                if (passkey is null)
                {
                    return Task.FromResult(IdentityResult.Failed());
                }

                found = true;
                passkey.Name = name.Trim();
                return _users.UpdateAsync(changed, cancellationToken);
            },
            cancellationToken
        );

        if (!found)
        {
            return new PasskeyOutcome { Kind = PasskeyOutcomeKind.NotFound };
        }

        return result.Succeeded
            ? new PasskeyOutcome { Kind = PasskeyOutcomeKind.Done, Name = name.Trim() }
            : new PasskeyOutcome { Kind = PasskeyOutcomeKind.Refused };
    }

    /// <summary>Removes a passkey, but never the user's last one.</summary>
    public async Task<PasskeyOutcome> RemoveAsync(int userId, byte[] credentialId, CancellationToken cancellationToken)
    {
        var user = await _users.FindByUserIdAsync(userId, cancellationToken);
        var passkeys = user?.Passkeys ?? [];
        if (!passkeys.Any(x => x.CredentialId.AsSpan().SequenceEqual(credentialId)))
        {
            return new PasskeyOutcome { Kind = PasskeyOutcomeKind.NotFound };
        }

        if (!Authentication.Passkeys.PasskeyRemovalRules.CanRemove(passkeys.Count))
        {
            return new PasskeyOutcome { Kind = PasskeyOutcomeKind.LastPasskey };
        }

        var removed = await _users.RemovePasskeyWhileAnotherRemainsAsync(userId, credentialId, cancellationToken);
        return new PasskeyOutcome { Kind = removed ? PasskeyOutcomeKind.Done : PasskeyOutcomeKind.LastPasskey };
    }

    /// <summary>An answer that cannot be checked is refused the same way whatever the reason. Cancellation is not.</summary>
    async Task<T?> TryCeremonyAsync<T>(Func<Task<T>> ceremony)
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

public sealed class PasskeySignIn
{
    public int UserId { get; init; }

    public string SessionToken { get; init; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; init; }
}

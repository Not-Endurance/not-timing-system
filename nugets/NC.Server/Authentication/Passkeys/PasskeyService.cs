using System.Buffers.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using NC.Server.Authentication.Email;
using NC.Server.Authentication.Identity;
using NC.Server.Authentication.Sessions;

namespace NC.Server.Authentication.Passkeys;

/// <summary>
/// Passkeys on top of ASP.NET Core Identity's passkey handler: the options for creating and using one, the check of the
/// browser's answer, and the management of the passkeys a user has. Every change goes through a concurrency-checked update.
/// </summary>
internal sealed class PasskeyService : IPasskeyService
{
    readonly IPasskeyCeremony _ceremony;
    readonly IPasskeyUsers _users;
    readonly ISessionRepository _sessions;
    readonly EmailDispatchQueue _mail;
    readonly TimeProvider _time;
    readonly ILogger<PasskeyService> _logger;

    public PasskeyService(
        IPasskeyCeremony ceremony,
        IPasskeyUsers users,
        ISessionRepository sessions,
        EmailDispatchQueue mail,
        TimeProvider time,
        ILogger<PasskeyService> logger
    )
    {
        _ceremony = ceremony;
        _users = users;
        _sessions = sessions;
        _mail = mail;
        _time = time;
        _logger = logger;
    }

    /// <summary>Options for a passkey sign-in. They name no user, so the authenticator offers the passkeys it holds.</summary>
    public Task<string> RequestOptions()
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

    /// <summary>Stores the passkey the browser created for the signed-in user, and tells them by email.</summary>
    public async Task<PasskeyOutcome> Enrol(
        int userId,
        string credentialJson,
        string? name,
        string language,
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
            _mail.Enqueue(EmailTemplates.PasskeyAdded(user.Email, language, passkey.Name!, now));
        }

        _logger.LogInformation("User {UserId} added a passkey.", userId);
        return new PasskeyOutcome { Kind = PasskeyOutcomeKind.Done, Name = passkey.Name };
    }

    /// <summary>Checks a passkey sign-in and starts a session for its user. Null when the answer is not accepted.</summary>
    public async Task<PasskeySignIn?> SignIn(string credentialJson, CancellationToken cancellationToken)
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
            // A session is bound to the stamp, so a user without one cannot be given a session.
            _logger.LogWarning("A passkey sign-in was refused: the user has no security stamp.");
            return null;
        }

        var now = _time.GetUtcNow();
        var issued = SessionToken.Issue();
        await _sessions.Create(issued.TokenHash, user.Id, user.SecurityStamp, now, cancellationToken);
        _logger.LogInformation("User {UserId} signed in with a passkey.", user.Id);

        return new PasskeySignIn
        {
            UserId = user.Id,
            SessionToken = issued.Token,
            ExpiresAtUtc = SessionRules.ExpiresAt(now),
        };
    }

    public async Task<IReadOnlyList<UserPasskeyInfo>> ListPasskeys(int userId, CancellationToken cancellationToken)
    {
        var user = await _users.FindByUserId(userId, cancellationToken);
        if (user is null)
        {
            return [];
        }

        return [.. (await _users.GetPasskeysAsync(user, cancellationToken)).OrderBy(x => x.CreatedAt)];
    }

    /// <summary>Renames one of the user's passkeys. The name is what the user sees on the Passkeys page to tell them apart.</summary>
    public async Task<PasskeyOutcome> Rename(
        int userId,
        byte[] credentialId,
        string name,
        CancellationToken cancellationToken
    )
    {
        var found = false;
        var result = await _users.Change(
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
    public async Task<PasskeyOutcome> Remove(int userId, byte[] credentialId, CancellationToken cancellationToken)
    {
        var user = await _users.FindByUserId(userId, cancellationToken);
        var passkeys = user?.Passkeys ?? [];
        if (!passkeys.Any(x => x.CredentialId.AsSpan().SequenceEqual(credentialId)))
        {
            return new PasskeyOutcome { Kind = PasskeyOutcomeKind.NotFound };
        }

        if (!PasskeyRemovalRules.CanRemove(passkeys.Count))
        {
            return new PasskeyOutcome { Kind = PasskeyOutcomeKind.LastPasskey };
        }

        var removed = await _users.RemovePasskeyWhileAnotherRemains(userId, credentialId, cancellationToken);
        return new PasskeyOutcome { Kind = removed ? PasskeyOutcomeKind.Done : PasskeyOutcomeKind.LastPasskey };
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

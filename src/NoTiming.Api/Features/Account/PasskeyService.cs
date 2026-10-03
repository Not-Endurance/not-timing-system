using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Not.Identity;
using Not.Identity.Email;

namespace NoTiming.Api.Features.Account;

/// <summary>
/// The passkey ceremonies and the rules around them (ADR-0002): a person adds passkeys while signed in, signs in with
/// one, and can never remove the last. Every change to a user goes through <c>ChangeAsync</c>, which starts again from
/// what is stored when another request changed the same user, so two passkeys added at once both stay.
/// </summary>
internal sealed class PasskeyService
{
    public const string LAST_PASSKEY = "LastPasskey";
    public const string NO_SUCH_PASSKEY = "NoSuchPasskey";

    readonly UserManager<NIdentityUser> _users;
    readonly SignInManager<NIdentityUser> _signIn;
    readonly IEmailSender _email;
    readonly AccountText _text;
    readonly TimeProvider _time;
    readonly ILogger<PasskeyService> _logger;

    public PasskeyService(
        UserManager<NIdentityUser> users,
        SignInManager<NIdentityUser> signIn,
        IEmailSender email,
        AccountText text,
        PasskeyAvailability availability,
        TimeProvider time,
        ILogger<PasskeyService> logger
    )
    {
        _users = users;
        _signIn = signIn;
        _email = email;
        _text = text;
        Availability = availability;
        _time = time;
        _logger = logger;
    }

    public PasskeyAvailability Availability { get; }

    /// <summary>The options for creating a passkey, as the JSON the browser's WebAuthn API takes.</summary>
    public async Task<string> CreationOptionsAsync(NIdentityUser user)
    {
        var id = user.Id.ToString();
        var email = user.Email ?? id;
        return await _signIn.MakePasskeyCreationOptionsAsync(
            new PasskeyUserEntity
            {
                Id = id,
                Name = email,
                DisplayName =
                    user.OtherFields != null && user.OtherFields.TryGetValue("Name", out var name) && name.IsString
                        ? name.AsString
                        : email,
            }
        );
    }

    /// <summary>
    /// The options for signing in. They name no account: a passkey is discoverable, so the browser offers the ones it
    /// has for this site, and nothing here tells whether an address has an account.
    /// </summary>
    public async Task<string> RequestOptionsAsync()
    {
        return await _signIn.MakePasskeyRequestOptionsAsync(null!);
    }

    /// <summary>Verifies what the authenticator made and stores the passkey for the signed-in user.</summary>
    public async Task<UserPasskeyInfo?> EnrolAsync(
        NIdentityUser user,
        string credentialJson,
        string? name,
        string language
    )
    {
        var attestation = await TryAsync(() => _signIn.PerformPasskeyAttestationAsync(credentialJson));
        if (attestation is null)
        {
            _logger.LogInformation("A passkey was refused for user {UserId}: no ceremony is underway.", user.Id);
            return null;
        }

        if (!attestation.Succeeded)
        {
            _logger.LogInformation(
                "A passkey was refused for user {UserId}: {Reason}",
                user.Id,
                attestation.Failure?.Message
            );
            return null;
        }

        // The options named the user the ceremony was for: it must be the one asking now.
        if (attestation.UserEntity.Id != user.Id.ToString())
        {
            _logger.LogWarning("A passkey made for another user was refused for user {UserId}.", user.Id);
            return null;
        }

        var passkey = attestation.Passkey;
        passkey.Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        var result = await _users.ChangeAsync(user.Id, changed => _users.AddOrUpdatePasskeyAsync(changed, passkey));
        if (!result.Succeeded)
        {
            _logger.LogWarning("A passkey could not be stored for user {UserId}.", user.Id);
            return null;
        }

        _logger.LogInformation("A passkey was added for user {UserId}.", user.Id);
        await NotifyAddedAsync(user, passkey, language);
        return passkey;
    }

    /// <summary>Verifies the assertion and signs the user in. Returns null for every way it can fail.</summary>
    public async Task<NIdentityUser?> SignInAsync(string credentialJson)
    {
        var assertion = await TryAsync(() => _signIn.PerformPasskeyAssertionAsync(credentialJson));
        if (assertion is null)
        {
            _logger.LogInformation("A passkey sign-in was refused: no ceremony is underway.");
            return null;
        }

        if (!assertion.Succeeded)
        {
            _logger.LogInformation("A passkey sign-in was refused: {Reason}", assertion.Failure?.Message);
            return null;
        }

        // The credential is replaced in place: its sign count moved.
        var stored = await _users.ChangeAsync(
            assertion.User.Id,
            changed => _users.AddOrUpdatePasskeyAsync(changed, assertion.Passkey)
        );
        if (!stored.Succeeded)
        {
            return null;
        }

        var user = await _users.FindByIdAsync(assertion.User.Id.ToString());
        if (user is null)
        {
            return null;
        }

        await _signIn.SignInAsync(user, isPersistent: true);
        _logger.LogInformation("User {UserId} signed in with a passkey.", user.Id);
        return user;
    }

    public async Task<IReadOnlyList<UserPasskeyInfo>> ListAsync(NIdentityUser user)
    {
        return [.. (await _users.GetPasskeysAsync(user)).OrderBy(x => x.CreatedAt)];
    }

    public async Task<IdentityResult> RenameAsync(Guid userId, byte[] credentialId, string? name)
    {
        return await _users.ChangeAsync(
            userId,
            async user =>
            {
                var passkey = await _users.GetPasskeyAsync(user, credentialId);
                if (passkey is null)
                {
                    return Failure(NO_SUCH_PASSKEY);
                }

                passkey.Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
                return await _users.AddOrUpdatePasskeyAsync(user, passkey);
            }
        );
    }

    /// <summary>
    /// Removes a passkey, never the last one: a person with no passkey left has only the email code, and the rule is
    /// the server's, not the page's.
    /// </summary>
    public async Task<IdentityResult> RemoveAsync(Guid userId, byte[] credentialId)
    {
        var result = await _users.ChangeAsync(
            userId,
            async user =>
            {
                var passkeys = await _users.GetPasskeysAsync(user);
                if (!passkeys.Any(x => x.CredentialId.AsSpan().SequenceEqual(credentialId)))
                {
                    return Failure(NO_SUCH_PASSKEY);
                }

                if (passkeys.Count <= 1)
                {
                    return Failure(LAST_PASSKEY);
                }

                return await _users.RemovePasskeyAsync(user, credentialId);
            }
        );
        if (result.Succeeded)
        {
            _logger.LogInformation("A passkey was removed for user {UserId}.", userId);
        }

        return result;
    }

    /// <summary>
    /// The ceremony runs on state the browser sends back in a short-lived cookie. Without it (it expired, or the
    /// request never asked for options) or with a body that is no credential, Identity throws: that is a refusal, not a
    /// failure of the host.
    /// </summary>
    static async Task<T?> TryAsync<T>(Func<Task<T>> ceremony)
        where T : class
    {
        try
        {
            return await ceremony();
        }
        catch (Exception ex) when (ex is InvalidOperationException or JsonException or FormatException)
        {
            return null;
        }
    }

    static IdentityResult Failure(string code)
    {
        return IdentityResult.Failed(new IdentityError { Code = code, Description = code });
    }

    async Task NotifyAddedAsync(NIdentityUser user, UserPasskeyInfo passkey, string language)
    {
        if (string.IsNullOrEmpty(user.Email))
        {
            return;
        }

        var values = new Dictionary<string, string>
        {
            ["name"] = string.IsNullOrWhiteSpace(passkey.Name) ? string.Empty : $" \"{passkey.Name}\"",
            ["when"] = _time.GetUtcNow().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture),
        };
        await _email.SendAsync(
            new EmailMessage(
                user.Email,
                _text.Get(language, "email.passkey.subject"),
                AccountText.Fill(_text.Get(language, "email.passkey.body"), values),
                language: language
            )
        );
    }
}

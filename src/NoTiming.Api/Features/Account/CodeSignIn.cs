using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Not.Identity;
using Not.Identity.Codes;
using Not.Identity.Email;
using NoTiming.Api.Features.Events;
using NTS.Domain.Aggregates;

namespace NoTiming.Api.Features.Account;

/// <summary>
/// Signing in with a code sent by email, and registering with one (ADR-0002, #601). Asking for a code answers alike
/// whether or not the address has an account, and nothing here is logged with a code or an address. A registration is
/// a sign-in that has the details of a new person waiting in the challenge: nothing is stored for them until the code
/// that proves the address has come back, and an address that has an account is simply signed in.
/// </summary>
internal sealed class CodeSignIn
{
    const int MAX_EMAIL_LENGTH = 254;

    public static bool IsValidEmail(string? email)
    {
        return !string.IsNullOrWhiteSpace(email)
            && email.Length <= MAX_EMAIL_LENGTH
            && MailAddress.TryCreate(email.Trim(), out var address)
            && address.Address == email.Trim();
    }

    readonly UserManager<NIdentityUser> _users;
    readonly SignInManager<NIdentityUser> _signIn;
    readonly ICodeChallengeStore _challenges;
    readonly IEmailSender _email;
    readonly SelectableCountries _countries;
    readonly TenantPlacement _tenants;
    readonly GrantInvitations _invitations;
    readonly RegistrationPolicy _policy;
    readonly EventThrottle _throttle;
    readonly AccountText _text;
    readonly NIdentityOptions _options;
    readonly ILogger<CodeSignIn> _logger;

    public CodeSignIn(
        UserManager<NIdentityUser> users,
        SignInManager<NIdentityUser> signIn,
        ICodeChallengeStore challenges,
        IEmailSender email,
        SelectableCountries countries,
        TenantPlacement tenants,
        GrantInvitations invitations,
        RegistrationPolicy policy,
        EventThrottle throttle,
        AccountText text,
        IOptions<NIdentityOptions> options,
        ILogger<CodeSignIn> logger
    )
    {
        _users = users;
        _signIn = signIn;
        _challenges = challenges;
        _email = email;
        _countries = countries;
        _tenants = tenants;
        _invitations = invitations;
        _policy = policy;
        _throttle = throttle;
        _text = text;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Sends a code to an address that has an account. For an address that has none it does nothing, and so does it
    /// inside the resend cooldown of the previous code: the caller cannot tell any of the three from the others.
    /// </summary>
    public async Task RequestAsync(string email, string language, CancellationToken cancellationToken)
    {
        var normalized = _users.NormalizeEmail(email)!;
        var user = await _users.FindByEmailAsync(normalized);
        if (user is null)
        {
            return;
        }

        await IssueAndSendAsync(normalized, null, language, user.Id, cancellationToken);
    }

    /// <summary>
    /// Sends a code to the address of a person who registers. An address that has an account is sent a sign-in code,
    /// as if it had been asked for on the sign-in page, and what was typed is dropped; a new address is sent a code
    /// with the details waiting in the challenge, unless the allow-list of the host keeps it from registering: then
    /// nothing is sent or kept. Whichever it was, the caller cannot tell.
    /// </summary>
    public async Task RequestRegistrationAsync(
        string email,
        RegistrationDetails details,
        string registrationToken,
        string language,
        CancellationToken cancellationToken
    )
    {
        var normalized = _users.NormalizeEmail(email)!;
        var user = await _users.FindByEmailAsync(normalized);
        if (user is null && !_policy.Allows(normalized))
        {
            if (_throttle.ShouldLog(AuthEvents.REGISTRATION_CLOSED.Name!, out var leftOut))
            {
                _logger.LogInformation(
                    AuthEvents.REGISTRATION_CLOSED,
                    "A registration was not accepted: its address is not on the allow-list. {LeftOut} refused since the last report.",
                    leftOut
                );
            }

            return;
        }

        await IssueAndSendAsync(
            normalized,
            user is null ? details.ToPending(RegistrationTokens.Hash(registrationToken)) : null,
            language,
            user?.Id,
            cancellationToken
        );
    }

    /// <summary>
    /// Verifies the code and, when it is right, signs the user in: the address is confirmed, a security stamp exists,
    /// and the session cookie is set. Returns null for every way the code can be wrong.
    /// </summary>
    public async Task<NIdentityUser?> VerifyAsync(
        string email,
        string code,
        string? registrationToken,
        CancellationToken cancellationToken
    )
    {
        var normalized = _users.NormalizeEmail(email)!;
        var verification = await _challenges.VerifyAsync(normalized, CodePurposes.SIGN_IN, code, cancellationToken);
        if (!verification.Succeeded)
        {
            _logger.LogInformation(AuthEvents.CODE_REFUSED, "A sign-in code was refused.");
            return null;
        }

        var user = await ConfirmAsync(normalized);
        if (user is null && verification.Pending != null)
        {
            user = await CreateAccountAsync(normalized, verification.Pending, registrationToken, cancellationToken);
        }

        if (user is null)
        {
            return null;
        }

        _logger.LogInformation(AuthEvents.CODE_VERIFIED, "A sign-in code was verified for user {UserId}.", user.Id);
        await _tenants.EnsureHomeTenantAsync(user, cancellationToken);

        // The address has just been proved, so the invitations that were waiting for it are the person's now.
        await _invitations.AttachAsync(normalized, user.Id, cancellationToken);

        // The stamp exists before the session does: rotating it is what ends the sessions of the user.
        await _signIn.SignInAsync(user, isPersistent: true);
        _logger.LogInformation(AuthEvents.SIGNED_IN, "User {UserId} signed in with a code.", user.Id);
        return user;
    }

    async Task IssueAndSendAsync(
        string normalized,
        BsonDocument? pending,
        string language,
        Guid? userId,
        CancellationToken cancellationToken
    )
    {
        var issue = await _challenges.IssueAsync(normalized, CodePurposes.SIGN_IN, pending, cancellationToken);
        if (!issue.Issued)
        {
            return;
        }

        var values = new Dictionary<string, string>
        {
            ["code"] = issue.Code!,
            ["minutes"] = ((int)_options.CodeLifetime.TotalMinutes).ToString(),
        };
        try
        {
            await _email.SendAsync(
                new EmailMessage(
                    normalized,
                    _text.Get(language, "email.code.subject"),
                    AccountText.Fill(_text.Get(language, "email.code.body"), values),
                    language: language
                ),
                cancellationToken
            );
        }
        catch (EmailDeliveryException ex)
        {
            // The provider refused it or did not answer. The person is told what everyone is told, and asks again
            // when the cooldown is over; what is logged holds no code and no address, which the message never has.
            _logger.LogError(
                AuthEvents.CODE_DELIVERY_FAILED,
                "A sign-in code could not be delivered: {Reason}",
                ex.Message
            );
            return;
        }

        _logger.LogInformation(
            AuthEvents.CODE_REQUESTED,
            "A sign-in code was sent to user {UserId}.",
            userId?.ToString() ?? "(not yet registered)"
        );
    }

    /// <summary>
    /// The account of a person whose address has just been proved: confirmed, with the names they typed, their country
    /// as the profile keeps it, and a home Tenant and one membership in it from the country. The details are used only
    /// when they were typed in the browser that now proves the address; otherwise the account is made without them,
    /// and the person is no worse off than one who skipped them.
    /// </summary>
    async Task<NIdentityUser?> CreateAccountAsync(
        string normalizedEmail,
        BsonDocument pending,
        string? registrationToken,
        CancellationToken cancellationToken
    )
    {
        var details = await RegistrationDetails.ReadAsync(pending, _countries, registrationToken, cancellationToken);
        if (details is null)
        {
            _logger.LogWarning(
                AuthEvents.REGISTRATION_DETAILS_DROPPED,
                "The details a registration left were not used: they were typed in another browser or are no longer valid."
            );
        }

        var fields = new BsonDocument { ["Roles"] = new BsonArray() };
        if (details != null)
        {
            fields["GivenName"] = details.GivenName;
            fields["Surname"] = details.Surname;
            fields["Name"] = $"{details.GivenName} {details.Surname}";
            if (details.Country is { } country)
            {
                fields["CountryRegion"] = country.Name;
                fields.AddRange(await _tenants.PlaceInAsync(Tenant.ForCountry(country), cancellationToken));
            }
        }

        var user = new NIdentityUser
        {
            Email = normalizedEmail,
            EmailConfirmed = true,
            OtherFields = fields,
        };
        var result = await _users.CreateAsync(user);
        if (result.Succeeded)
        {
            _logger.LogInformation(AuthEvents.REGISTERED, "User {UserId} registered.", user.Id);
            return user;
        }

        // The address was registered by someone else while the code was on its way. The code proves it is theirs too.
        return await _users.FindByEmailAsync(normalizedEmail);
    }

    async Task<NIdentityUser?> ConfirmAsync(string normalizedEmail)
    {
        // One retry: another request may have updated the same user between the read and the write.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var user = await _users.FindByEmailAsync(normalizedEmail);
            if (user is null)
            {
                return null;
            }

            var changed = false;
            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                changed = true;
            }

            IdentityResult result;
            if (string.IsNullOrEmpty(user.SecurityStamp))
            {
                result = await _users.UpdateSecurityStampAsync(user);
            }
            else if (changed)
            {
                result = await _users.UpdateAsync(user);
            }
            else
            {
                return user;
            }

            if (result.Succeeded)
            {
                return user;
            }
        }

        throw new InvalidOperationException("The user could not be updated to sign in: it keeps changing.");
    }
}

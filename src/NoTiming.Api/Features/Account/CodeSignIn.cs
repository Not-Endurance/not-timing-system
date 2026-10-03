using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Not.Identity;
using Not.Identity.Codes;
using Not.Identity.Email;

namespace NoTiming.Api.Features.Account;

/// <summary>
/// Signing in with a code sent by email (ADR-0002). Asking for a code answers alike whether or not the address has an
/// account, and nothing here is logged with a code or an address.
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
    readonly AccountText _text;
    readonly NIdentityOptions _options;
    readonly ILogger<CodeSignIn> _logger;

    public CodeSignIn(
        UserManager<NIdentityUser> users,
        SignInManager<NIdentityUser> signIn,
        ICodeChallengeStore challenges,
        IEmailSender email,
        AccountText text,
        IOptions<NIdentityOptions> options,
        ILogger<CodeSignIn> logger
    )
    {
        _users = users;
        _signIn = signIn;
        _challenges = challenges;
        _email = email;
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

        var issue = await _challenges.IssueAsync(
            normalized,
            CodePurposes.SIGN_IN,
            cancellationToken: cancellationToken
        );
        if (!issue.Issued)
        {
            return;
        }

        var values = new Dictionary<string, string>
        {
            ["code"] = issue.Code!,
            ["minutes"] = ((int)_options.CodeLifetime.TotalMinutes).ToString(),
        };
        await _email.SendAsync(
            new EmailMessage(
                normalized,
                _text.Get(language, "email.code.subject"),
                AccountText.Fill(_text.Get(language, "email.code.body"), values),
                language: language
            ),
            cancellationToken
        );
        _logger.LogInformation("A sign-in code was sent to user {UserId}.", user.Id);
    }

    /// <summary>
    /// Verifies the code and, when it is right, signs the user in: the address is confirmed, a security stamp exists,
    /// and the session cookie is set. Returns null for every way the code can be wrong.
    /// </summary>
    public async Task<NIdentityUser?> VerifyAsync(string email, string code, CancellationToken cancellationToken)
    {
        var normalized = _users.NormalizeEmail(email)!;
        var verification = await _challenges.VerifyAsync(normalized, CodePurposes.SIGN_IN, code, cancellationToken);
        if (!verification.Succeeded)
        {
            _logger.LogInformation("A sign-in code was refused.");
            return null;
        }

        var user = await ConfirmAsync(normalized);
        if (user is null)
        {
            return null;
        }

        // The stamp exists before the session does: rotating it is what ends the sessions of the user.
        await _signIn.SignInAsync(user, isPersistent: true);
        _logger.LogInformation("User {UserId} signed in with a code.", user.Id);
        return user;
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

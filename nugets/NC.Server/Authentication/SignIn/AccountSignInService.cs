using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using NC.Server.Authentication.Adoption;
using NC.Server.Authentication.Codes;
using NC.Server.Authentication.Email;
using NC.Server.Authentication.Identity;
using NC.Server.Authentication.RateLimits;
using NC.Server.Authentication.Settings;

namespace NC.Server.Authentication.SignIn;

/// <summary>
/// Sign-in and registration by a one-time code. A request makes a challenge whether or not the address has an account,
/// and sends mail only when there is an account (sign-in) or a registration (register). A check proves the address and
/// returns the user it belongs to. Starting the session is the caller's job, so every sign-in path shares it.
/// </summary>
internal sealed class AccountSignInService
{
    readonly IIdentityUserRepository _users;
    readonly ICodeChallengeRepository _challenges;
    readonly CodeSendGate _gate;
    readonly EmailDispatchQueue _mail;
    readonly IEnumerable<ILegacyUserAdoption> _adoptions;
    readonly IOptions<AuthenticationSettings> _settings;
    readonly TimeProvider _time;
    readonly ILogger<AccountSignInService> _logger;

    public AccountSignInService(
        IIdentityUserRepository users,
        ICodeChallengeRepository challenges,
        CodeSendGate gate,
        EmailDispatchQueue mail,
        IEnumerable<ILegacyUserAdoption> adoptions,
        IOptions<AuthenticationSettings> settings,
        TimeProvider time,
        ILogger<AccountSignInService> logger
    )
    {
        _users = users;
        _challenges = challenges;
        _gate = gate;
        _mail = mail;
        _adoptions = adoptions;
        _settings = settings;
        _time = time;
        _logger = logger;
    }

    public async Task<CodeRequestOutcome> RequestSignInCode(
        string email,
        string language,
        string clientAddress,
        CancellationToken cancellationToken
    )
    {
        var address = EmailAddress.Normalize(email);
        var now = _time.GetUtcNow();
        var decision = await _gate.Review(address, clientAddress, now, cancellationToken);
        if (!decision.Allowed)
        {
            return Refused(decision.RetryAfter);
        }

        var code = OneTimeCodes.Generate();
        await StoreChallenge(address, CodeChallengePurpose.SIGN_IN, registration: null, code, now, cancellationToken);
        await _gate.Record(address, clientAddress, now, cancellationToken);

        // Mail goes out only for an address that has an account. The response is the same either way.
        var accounts = await _users.FindAllByEmail(address, cancellationToken);
        if (accounts.Count > 0)
        {
            _mail.Enqueue(EmailTemplates.SignInCode(address, language, code));
        }

        return Accepted();
    }

    public async Task<CodeRequestOutcome> RequestRegistrationCode(
        string email,
        RegistrationDetails details,
        string language,
        string clientAddress,
        CancellationToken cancellationToken
    )
    {
        var address = EmailAddress.Normalize(email);
        var now = _time.GetUtcNow();
        var decision = await _gate.Review(address, clientAddress, now, cancellationToken);
        if (!decision.Allowed)
        {
            return Refused(decision.RetryAfter);
        }

        var code = OneTimeCodes.Generate();
        var registration = new RegistrationDetailsDocument
        {
            GivenName = details.GivenName.Trim(),
            Surname = details.Surname.Trim(),
            CountryRegion = details.CountryRegion.Trim(),
        };
        await StoreChallenge(address, CodeChallengePurpose.REGISTER, registration, code, now, cancellationToken);
        await _gate.Record(address, clientAddress, now, cancellationToken);
        _mail.Enqueue(EmailTemplates.RegistrationCode(address, language, code));

        return Accepted();
    }

    /// <summary>Checks a code. Returns the user it signs in, or null when the code is not accepted or no user can be found.</summary>
    public async Task<IdentityUserDocument?> VerifyCode(string email, string code, CancellationToken cancellationToken)
    {
        var address = EmailAddress.Normalize(email);
        var now = _time.GetUtcNow();

        // The attempt is spent before the code is compared, so a guess is counted even when it is right.
        var spent = await _challenges.SpendAttempt(address, now, cancellationToken);
        if (spent is null)
        {
            return null;
        }

        var matches = CryptographicOperations.FixedTimeEquals(CodeMac.Compute(Secret(), address, code), spent.Mac);
        var before = new CodeChallengeState
        {
            ExpiresAtUtc = new DateTimeOffset(spent.ExpiresAtUtc, TimeSpan.Zero),
            AttemptsLeft = spent.AttemptsLeft + 1,
            Used = false,
        };
        if (!CodeChallengeRules.Verify(before, matches, now).Accepted)
        {
            _logger.LogInformation("A sign-in code was refused.");
            return null;
        }

        // A correct code is deleted atomically, so it signs in once even if two requests check it together.
        if (!await _challenges.Consume(address, spent.Mac, cancellationToken))
        {
            return null;
        }

        return await ResolveUser(address, spent, now, cancellationToken);
    }

    async Task<IdentityUserDocument?> ResolveUser(
        string address,
        CodeChallengeDocument challenge,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        var matches = await _users.FindAllByEmail(address, cancellationToken);
        if (matches.Count > 1)
        {
            _logger.LogWarning("A sign-in was refused: more than one user record matches the address.");
            return null;
        }

        if (matches.Count == 1)
        {
            var existing = matches[0];
            if (existing.IsProved)
            {
                return await EnsureStamp(existing, cancellationToken);
            }

            foreach (var adoption in _adoptions)
            {
                var adopted = await adoption.Adopt(existing, now, cancellationToken);
                if (adopted is not null)
                {
                    return adopted;
                }
            }

            return null;
        }

        if (challenge.Purpose == CodeChallengePurpose.REGISTER && challenge.Registration is { } details)
        {
            try
            {
                return await _users.CreateProvedUser(
                    address,
                    details.GivenName,
                    details.Surname,
                    details.CountryRegion,
                    now,
                    cancellationToken
                );
            }
            catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
            {
                _logger.LogWarning("A registration was refused: the address was taken meanwhile.");
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// Gives a user without a stamp one. When the stamp cannot be saved the sign-in is refused: the session would be bound
    /// to a stamp that the stored user does not have, and the session would end on its next request.
    /// </summary>
    async Task<IdentityUserDocument?> EnsureStamp(IdentityUserDocument user, CancellationToken cancellationToken)
    {
        if (user.SecurityStamp is not null)
        {
            return user;
        }

        user.SecurityStamp = Guid.NewGuid().ToString("N");
        var saved = await _users.UpdateAsync(user, cancellationToken);
        if (!saved.Succeeded)
        {
            _logger.LogWarning("A sign-in was refused: the security stamp could not be saved.");
            return null;
        }

        return user;
    }

    async Task StoreChallenge(
        string address,
        string purpose,
        RegistrationDetailsDocument? registration,
        string code,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        var state = CodeChallengeRules.Issue(now);
        await _challenges.Replace(
            new CodeChallengeDocument
            {
                Email = address,
                Purpose = purpose,
                Mac = CodeMac.Compute(Secret(), address, code),
                AttemptsLeft = state.AttemptsLeft,
                LastSentAtUtc = now.UtcDateTime,
                ExpiresAtUtc = state.ExpiresAtUtc.UtcDateTime,
                Registration = registration,
            },
            cancellationToken
        );
    }

    byte[] Secret()
    {
        return Encoding.UTF8.GetBytes(_settings.Value.CodeSecret);
    }

    static CodeRequestOutcome Accepted()
    {
        return new CodeRequestOutcome { Accepted = true, RetryAfter = TimeSpan.Zero };
    }

    static CodeRequestOutcome Refused(TimeSpan retryAfter = default)
    {
        return new CodeRequestOutcome { Accepted = false, RetryAfter = retryAfter };
    }
}

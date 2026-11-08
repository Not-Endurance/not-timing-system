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
using NC.Server.Authentication.Sessions;
using NC.Server.Authentication.Store;

namespace NC.Server.Authentication.SignIn;

/// <summary>
/// Sign-in and registration by a one-time code. A request makes a challenge whether or not the address has an account,
/// and sends mail only when there is an account (sign-in) or a registration (register). A check proves the address,
/// then branches on the users that match it.
/// </summary>
public sealed class AccountSignInService
{
    readonly IdentityUserStore _users;
    readonly SessionStore _sessions;
    readonly CodeChallengeStore _challenges;
    readonly CodeSendGate _gate;
    readonly EmailDispatchQueue _mail;
    readonly IEnumerable<ILegacyUserAdoption> _adoptions;
    readonly IOptions<AuthenticationSettings> _settings;
    readonly TimeProvider _time;
    readonly ILogger<AccountSignInService> _logger;

    public AccountSignInService(
        IdentityUserStore users,
        SessionStore sessions,
        CodeChallengeStore challenges,
        CodeSendGate gate,
        EmailDispatchQueue mail,
        IEnumerable<ILegacyUserAdoption> adoptions,
        IOptions<AuthenticationSettings> settings,
        TimeProvider time,
        ILogger<AccountSignInService> logger
    )
    {
        _users = users;
        _sessions = sessions;
        _challenges = challenges;
        _gate = gate;
        _mail = mail;
        _adoptions = adoptions;
        _settings = settings;
        _time = time;
        _logger = logger;
    }

    public async Task<CodeRequestOutcome> RequestSignInCodeAsync(
        string email,
        string language,
        string clientAddress,
        CancellationToken cancellationToken
    )
    {
        var address = EmailAddress.Normalize(email);
        var now = _time.GetUtcNow();
        var decision = await _gate.ReviewAsync(address, clientAddress, now, cancellationToken);
        if (!decision.Allowed)
        {
            return Refused(decision.RetryAfter);
        }

        var code = OneTimeCodes.Generate();
        await StoreChallengeAsync(
            address,
            CodeChallengeStore.SIGN_IN,
            registration: null,
            code,
            now,
            cancellationToken
        );
        await _gate.RecordAsync(address, clientAddress, now, cancellationToken);

        // Mail goes out only for an address that has an account. The response is the same either way.
        var accounts = await _users.FindAllByEmailAsync(address, cancellationToken);
        if (accounts.Count > 0)
        {
            _mail.Enqueue(EmailTemplates.SignInCode(address, language, code));
        }

        return Accepted();
    }

    public async Task<CodeRequestOutcome> RequestRegistrationCodeAsync(
        string email,
        RegistrationDetails details,
        string language,
        string clientAddress,
        CancellationToken cancellationToken
    )
    {
        var address = EmailAddress.Normalize(email);
        var now = _time.GetUtcNow();
        var decision = await _gate.ReviewAsync(address, clientAddress, now, cancellationToken);
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
        await StoreChallengeAsync(address, CodeChallengeStore.REGISTER, registration, code, now, cancellationToken);
        await _gate.RecordAsync(address, clientAddress, now, cancellationToken);
        _mail.Enqueue(EmailTemplates.RegistrationCode(address, language, code));

        return Accepted();
    }

    public async Task<VerificationOutcome> VerifyCodeAsync(
        string email,
        string code,
        CancellationToken cancellationToken
    )
    {
        var address = EmailAddress.Normalize(email);
        var now = _time.GetUtcNow();

        // The attempt is spent before the code is compared, so a guess is counted even when it is right.
        var spent = await _challenges.SpendAttemptAsync(address, now, cancellationToken);
        if (spent is null)
        {
            return Refused();
        }

        var matches = CryptographicOperations.FixedTimeEquals(CodeMac.Compute(Secret(), address, code), spent.Mac);
        var before = new CodeChallengeState
        {
            ExpiresAtUtc = new DateTimeOffset(spent.ExpiresAt, TimeSpan.Zero),
            AttemptsLeft = spent.AttemptsLeft + 1,
            Used = false,
        };
        if (!CodeChallengeRules.Verify(before, matches, now).Accepted)
        {
            _logger.LogInformation("A sign-in code was refused.");
            return Refused();
        }

        // A correct code is deleted atomically, so it signs in once even if two requests check it together.
        if (!await _challenges.ConsumeAsync(address, spent.Mac, cancellationToken))
        {
            return Refused();
        }

        var user = await ResolveUserAsync(address, spent, now, cancellationToken);
        if (user is null)
        {
            return Refused();
        }

        var issued = SessionToken.Issue();
        await _sessions.CreateAsync(issued.TokenHash, user.Id, user.SecurityStamp!, now, cancellationToken);
        _logger.LogInformation("User {UserId} signed in by code.", user.Id);

        return new VerificationOutcome
        {
            SignedIn = true,
            UserId = user.Id,
            SessionToken = issued.Token,
            ExpiresAtUtc = SessionRules.ExpiresAt(now),
        };
    }

    async Task<IdentityUserDocument?> ResolveUserAsync(
        string address,
        CodeChallengeDocument challenge,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        var matches = await _users.FindAllByEmailAsync(address, cancellationToken);
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
                return await EnsureStampAsync(existing, cancellationToken);
            }

            foreach (var adoption in _adoptions)
            {
                var adopted = await adoption.AdoptAsync(existing, now, cancellationToken);
                if (adopted is not null)
                {
                    return adopted;
                }
            }

            return null;
        }

        if (challenge.Purpose == CodeChallengeStore.REGISTER && challenge.Registration is { } details)
        {
            try
            {
                return await _users.CreateProvedUserAsync(
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

    async Task<IdentityUserDocument> EnsureStampAsync(IdentityUserDocument user, CancellationToken cancellationToken)
    {
        if (user.SecurityStamp is not null)
        {
            return user;
        }

        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await _users.UpdateAsync(user, cancellationToken);
        return user;
    }

    async Task StoreChallengeAsync(
        string address,
        string purpose,
        RegistrationDetailsDocument? registration,
        string code,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        var state = CodeChallengeRules.Issue(now);
        await _challenges.ReplaceAsync(
            new CodeChallengeDocument
            {
                Email = address,
                Purpose = purpose,
                Mac = CodeMac.Compute(Secret(), address, code),
                AttemptsLeft = state.AttemptsLeft,
                LastSentAt = now.UtcDateTime,
                ExpiresAt = state.ExpiresAtUtc.UtcDateTime,
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

    static VerificationOutcome Refused()
    {
        return new VerificationOutcome { SignedIn = false };
    }
}

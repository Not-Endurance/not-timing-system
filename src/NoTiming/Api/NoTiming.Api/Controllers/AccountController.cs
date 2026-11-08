using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NC.Server.Authentication.Account;
using NC.Server.Authentication.Email;
using NC.Server.Authentication.Identity;
using NC.Server.Authentication.Requests;
using NC.Server.Authentication.Sessions;
using NC.Server.Authentication.Settings;
using NC.Server.Authentication.SignIn;

namespace NoTiming.Api.Controllers;

/// <summary>The anonymous and signed-in routes of sign-in, registration, the session and the profile.</summary>
[ApiController]
[Route("api")]
[ServiceFilter(typeof(StateChangeGuard))]
public sealed class AccountController : ControllerBase
{
    readonly AccountSignInService _signIn;
    readonly AccountService _account;
    readonly EmailLanguage _languages;
    readonly IOptions<AuthenticationSettings> _settings;

    public AccountController(
        AccountSignInService signIn,
        AccountService account,
        EmailLanguage languages,
        IOptions<AuthenticationSettings> settings
    )
    {
        _signIn = signIn;
        _account = account;
        _languages = languages;
        _settings = settings;
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        return Ok(user is null ? MeResponse.Anonymous() : MeResponse.For(user));
    }

    /// <summary>The signed-in user's identity for the Ui. Private: the anonymous /me answers only the signed-in state.</summary>
    [HttpGet("me/profile")]
    public async Task<IActionResult> Profile(CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync(cancellationToken);
        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(
            new ProfileResponse
            {
                Id = user.Id,
                Email = user.Email,
                Name = user.Name,
                DisplayName = user.DisplayName,
                GivenName = user.GivenName,
                MiddleName = user.MiddleName,
                Surname = user.Surname,
                CountryRegion = user.CountryRegion,
                Club = user.Club,
                FeiId = user.FeiId,
                Roles = user.Roles,
            }
        );
    }

    [HttpPatch("me")]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] ProfileRequest request,
        CancellationToken cancellationToken
    )
    {
        var userId = await SessionUser.IdOf(HttpContext);
        if (userId is null)
        {
            return Unauthorized();
        }

        var saved = await _account.UpdateProfile(userId.Value, request.ToChanges(), cancellationToken);
        return saved ? NoContent() : BadRequest(new { error = "missing-details" });
    }

    [HttpPost("sign-in/code")]
    public async Task<IActionResult> RequestSignInCode(
        [FromBody] EmailRequest request,
        CancellationToken cancellationToken
    )
    {
        if (!IsPlausibleEmail(request.Email))
        {
            return BadRequest(new { error = "invalid-email" });
        }

        var outcome = await _signIn.RequestSignInCode(request.Email!, Language(), ClientAddress(), cancellationToken);
        return CodeRequestAnswer(outcome);
    }

    /// <summary>A filled-in honeypot gets the normal answer and nothing else.</summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.Website))
        {
            return Accepted();
        }

        if (
            !IsPlausibleEmail(request.Email)
            || string.IsNullOrWhiteSpace(request.GivenName)
            || string.IsNullOrWhiteSpace(request.Surname)
            || string.IsNullOrWhiteSpace(request.CountryRegion)
        )
        {
            return BadRequest(new { error = "missing-details" });
        }

        var details = new RegistrationDetails
        {
            GivenName = request.GivenName!,
            Surname = request.Surname!,
            CountryRegion = request.CountryRegion!,
        };
        var outcome = await _signIn.RequestRegistrationCode(
            request.Email!,
            details,
            Language(),
            ClientAddress(),
            cancellationToken
        );
        return CodeRequestAnswer(outcome);
    }

    /// <summary>Every failure gets the same answer: wrong, expired, used up or missing.</summary>
    [HttpPost("sign-in/verify")]
    public async Task<IActionResult> VerifyCode([FromBody] VerifyRequest request, CancellationToken cancellationToken)
    {
        var outcome = await _signIn.VerifyCode(
            request.Email ?? string.Empty,
            request.Code ?? string.Empty,
            cancellationToken
        );
        if (!outcome.SignedIn || outcome.SessionToken is null)
        {
            return Unauthorized(new { error = "code-not-accepted" });
        }

        Response.Cookies.Append(
            _settings.Value.CookieName,
            outcome.SessionToken,
            SessionCookie.Options(outcome.ExpiresAtUtc)
        );
        var user = await _account.Find(outcome.UserId, cancellationToken);
        return Ok(user is null ? MeResponse.Anonymous() : MeResponse.For(user));
    }

    [HttpPost("sign-out")]
    public async Task<IActionResult> SignOut(CancellationToken cancellationToken)
    {
        var name = _settings.Value.CookieName;
        await _account.SignOut(Request.Cookies[name], cancellationToken);
        Response.Cookies.Append(name, string.Empty, SessionCookie.Options(DateTimeOffset.UnixEpoch));
        return NoContent();
    }

    async Task<IdentityUserDocument?> CurrentUserAsync(CancellationToken cancellationToken)
    {
        var userId = await SessionUser.IdOf(HttpContext);
        return userId is null ? null : await _account.Find(userId.Value, cancellationToken);
    }

    string Language()
    {
        return _languages.Choose(Request.Headers.AcceptLanguage.ToString());
    }

    string ClientAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    IActionResult CodeRequestAnswer(CodeRequestOutcome outcome)
    {
        if (outcome.Accepted)
        {
            return Accepted();
        }

        var seconds = Math.Max(1, (int)Math.Ceiling(outcome.RetryAfter.TotalSeconds));
        Response.Headers.RetryAfter = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return StatusCode(StatusCodes.Status429TooManyRequests, new { error = "too-many-requests" });
    }

    static bool IsPlausibleEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 254)
        {
            return false;
        }

        var address = email.Trim();
        var at = address.IndexOf('@');
        return at > 0 && at < address.Length - 3 && address.IndexOf('@', at + 1) < 0;
    }
}

public sealed class EmailRequest
{
    public string? Email { get; init; }
}

public sealed class RegisterRequest
{
    public string? Email { get; init; }

    public string? GivenName { get; init; }

    public string? Surname { get; init; }

    public string? CountryRegion { get; init; }

    /// <summary>A field no person fills in. A bot does, and gets the normal answer and nothing else.</summary>
    public string? Website { get; init; }
}

public sealed class VerifyRequest
{
    public string? Email { get; init; }

    public string? Code { get; init; }
}

public sealed class ProfileRequest
{
    public string? GivenName { get; init; }

    public string? MiddleName { get; init; }

    public string? Surname { get; init; }

    public string? CountryRegion { get; init; }

    public string? Club { get; init; }

    public string? FeiId { get; init; }

    public ProfileChanges ToChanges()
    {
        return new ProfileChanges
        {
            GivenName = GivenName ?? string.Empty,
            MiddleName = MiddleName,
            Surname = Surname ?? string.Empty,
            CountryRegion = CountryRegion ?? string.Empty,
            Club = Club,
            FeiId = FeiId,
        };
    }
}

public sealed class ProfileResponse
{
    public int Id { get; init; }

    public string? Email { get; init; }

    public string? Name { get; init; }

    public string? DisplayName { get; init; }

    public string? GivenName { get; init; }

    public string? MiddleName { get; init; }

    public string? Surname { get; init; }

    public string? CountryRegion { get; init; }

    public string? Club { get; init; }

    public string? FeiId { get; init; }

    public string[] Roles { get; init; } = [];
}

public sealed class MeResponse
{
    public static MeResponse Anonymous()
    {
        return new MeResponse { SignedIn = false };
    }

    public static MeResponse For(IdentityUserDocument user)
    {
        return new MeResponse
        {
            SignedIn = true,
            DisplayName = user.DisplayName ?? user.Name ?? user.Email,
            HasPasskey = user.Passkeys is { Count: > 0 },
        };
    }

    public bool SignedIn { get; init; }

    public string? DisplayName { get; init; }

    public bool HasPasskey { get; init; }
}

using Microsoft.AspNetCore.Mvc;
using NC.Server.Authentication;
using NC.Server.Authentication.Account;
using NC.Server.Authentication.Requests;
using NC.Server.Authentication.SignIn;

namespace NoTiming.Api.Controllers;

/// <summary>The anonymous and signed-in routes of sign-in, registration, the session and the profile.</summary>
[ApiController]
[Route("api")]
[ServiceFilter(typeof(StateChangeGuard))]
public sealed class AccountController : ControllerBase
{
    readonly IAuthentication _authentication;

    public AccountController(IAuthentication authentication)
    {
        _authentication = authentication;
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var user = await _authentication.CurrentUser(HttpContext, cancellationToken);
        return Ok(user is null ? MeResponse.Anonymous() : MeResponse.For(user));
    }

    /// <summary>The signed-in user's identity for the Ui. Private: the anonymous /me answers only the signed-in state.</summary>
    [HttpGet("me/profile")]
    public async Task<IActionResult> Profile(CancellationToken cancellationToken)
    {
        var user = await _authentication.CurrentUser(HttpContext, cancellationToken);
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
        var outcome = await _authentication.UpdateProfile(HttpContext, request.ToChanges(), cancellationToken);
        return outcome switch
        {
            ProfileUpdateOutcome.Saved => NoContent(),
            ProfileUpdateOutcome.NotSignedIn => Unauthorized(),
            _ => BadRequest(new { error = "missing-details" }),
        };
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

        var outcome = await _authentication.SendSignInCode(HttpContext, request.Email!, cancellationToken);
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
        var outcome = await _authentication.RequestRegistrationCode(
            HttpContext,
            request.Email!,
            details,
            cancellationToken
        );
        return CodeRequestAnswer(outcome);
    }

    /// <summary>Every failure gets the same answer: wrong, expired, used up or missing.</summary>
    [HttpPost("sign-in/verify")]
    public async Task<IActionResult> VerifyCode([FromBody] VerifyRequest request, CancellationToken cancellationToken)
    {
        var outcome = await _authentication.SignInWithCode(
            HttpContext,
            request.Email ?? string.Empty,
            request.Code ?? string.Empty,
            cancellationToken
        );
        return outcome.SignedIn && outcome.User is not null
            ? Ok(MeResponse.For(outcome.User))
            : Unauthorized(new { error = "code-not-accepted" });
    }

    [HttpPost("sign-out")]
    public async Task<IActionResult> SignOut(CancellationToken cancellationToken)
    {
        await _authentication.SignOut(HttpContext, cancellationToken);
        return NoContent();
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

    public static MeResponse For(AuthenticatedUser user)
    {
        return new MeResponse
        {
            SignedIn = true,
            DisplayName = user.DisplayName ?? user.Name ?? user.Email,
            HasPasskey = user.HasPasskey,
        };
    }

    public bool SignedIn { get; init; }

    public string? DisplayName { get; init; }

    public bool HasPasskey { get; init; }
}

using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NC.Server.Authentication.Interface;
using NC.Server.Authentication.Profiles;
using NC.Server.Authentication.Requests;
using NC.Server.Authentication.SignIn;

namespace NC.Server.Authentication.Account;

/// <summary>The anonymous and signed-in routes of sign-in, registration, the session and the profile.</summary>
internal static class AccountEndpoints<TProfile>
    where TProfile : class, new()
{
    internal static void Map(RouteGroupBuilder api)
    {
        api.MapGet("/me", Me);
        api.MapGet("/me/profile", Profile).RequireAuthorization(AuthenticationPolicies.SIGNED_IN);
        api.MapPatch("/me", UpdateProfile)
            .RequireAuthorization(AuthenticationPolicies.SIGNED_IN)
            .AddEndpointFilter<StateChangeGuard>();
        api.MapPost("/sign-in/code", RequestSignInCode).AddEndpointFilter<StateChangeGuard>();
        api.MapPost("/register", Register).AddEndpointFilter<StateChangeGuard>();
        api.MapPost("/sign-in/verify", VerifyCode).AddEndpointFilter<StateChangeGuard>();
        api.MapPost("/sign-out", SignOut).AddEndpointFilter<StateChangeGuard>();
    }

    static async Task<IResult> Me(
        ClaimsPrincipal principal,
        AccountProfiles<TProfile> profiles,
        CancellationToken cancellationToken
    )
    {
        var userId = principal.GetSignedInUserId();
        var user = userId is null ? null : await profiles.Find(userId.Value, cancellationToken);
        return Results.Ok(user is null ? MeResponse.Anonymous() : MeResponse.For(user));
    }

    /// <summary>The signed-in user's identity and profile for the Ui.</summary>
    static async Task<IResult> Profile(
        ClaimsPrincipal principal,
        AccountProfiles<TProfile> profiles,
        CancellationToken cancellationToken
    )
    {
        var userId = principal.GetSignedInUserId();
        var user = userId is null ? null : await profiles.Find(userId.Value, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(
            new ProfileResponse
            {
                Id = user.Id,
                Email = user.Email,
                Name = user.Name,
                DisplayName = user.DisplayName,
                GivenName = user.GivenName,
                MiddleName = user.MiddleName,
                Surname = user.Surname,
                Roles = user.Roles,
                Profile = user.Profile,
            }
        );
    }

    static async Task<IResult> UpdateProfile(
        ProfileRequest request,
        ClaimsPrincipal principal,
        AccountProfiles<TProfile> profiles,
        ProfileRules<TProfile> rules,
        CancellationToken cancellationToken
    )
    {
        var userId = principal.GetSignedInUserId();
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        if (request.Profile is null || !rules.Accepts(request.Profile))
        {
            return Results.BadRequest(new { error = "missing-details" });
        }

        var outcome = await profiles.Update(userId.Value, request.ToChanges(), cancellationToken);
        return outcome == ProfileUpdateOutcome.Saved
            ? Results.NoContent()
            : Results.BadRequest(new { error = "missing-details" });
    }

    static async Task<IResult> RequestSignInCode(
        EmailRequest request,
        HttpContext httpContext,
        IAuthentication<TProfile> authentication,
        CancellationToken cancellationToken
    )
    {
        if (!IsPlausibleEmail(request.Email))
        {
            return Results.BadRequest(new { error = "invalid-email" });
        }

        var outcome = await authentication.SendSignInCode(httpContext, request.Email!, cancellationToken);
        return CodeRequestAnswer(httpContext, outcome);
    }

    /// <summary>A filled-in honeypot gets the normal answer and nothing else.</summary>
    static async Task<IResult> Register(
        RegisterRequest request,
        HttpContext httpContext,
        IAuthentication<TProfile> authentication,
        ProfileRules<TProfile> rules,
        CancellationToken cancellationToken
    )
    {
        if (!string.IsNullOrWhiteSpace(request.Website))
        {
            return Results.Accepted();
        }

        if (
            !IsPlausibleEmail(request.Email)
            || string.IsNullOrWhiteSpace(request.GivenName)
            || string.IsNullOrWhiteSpace(request.Surname)
            || request.Profile is null
            || !rules.Accepts(request.Profile)
        )
        {
            return Results.BadRequest(new { error = "missing-details" });
        }

        var details = new RegistrationDetails<TProfile>
        {
            GivenName = request.GivenName!,
            Surname = request.Surname!,
            Profile = request.Profile,
        };
        var outcome = await authentication.RequestRegistrationCode(
            httpContext,
            request.Email!,
            details,
            cancellationToken
        );
        return CodeRequestAnswer(httpContext, outcome);
    }

    /// <summary>Every failure gets the same answer: wrong, expired, used up or missing.</summary>
    static async Task<IResult> VerifyCode(
        VerifyRequest request,
        HttpContext httpContext,
        IAuthentication<TProfile> authentication,
        CancellationToken cancellationToken
    )
    {
        var outcome = await authentication.SignInWithCode(
            httpContext,
            request.Email ?? string.Empty,
            request.Code ?? string.Empty,
            cancellationToken
        );
        return outcome.SignedIn && outcome.User is not null
            ? Results.Ok(MeResponse.For(outcome.User))
            : Results.Json(new { error = "code-not-accepted" }, statusCode: StatusCodes.Status401Unauthorized);
    }

    static async Task<IResult> SignOut(
        HttpContext httpContext,
        IAuthentication<TProfile> authentication,
        CancellationToken cancellationToken
    )
    {
        await authentication.SignOut(httpContext, cancellationToken);
        return Results.NoContent();
    }

    static IResult CodeRequestAnswer(HttpContext httpContext, CodeRequestOutcome outcome)
    {
        if (outcome.Accepted)
        {
            return Results.Accepted();
        }

        var seconds = Math.Max(1, (int)Math.Ceiling(outcome.RetryAfter.TotalSeconds));
        httpContext.Response.Headers.RetryAfter = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return Results.Json(new { error = "too-many-requests" }, statusCode: StatusCodes.Status429TooManyRequests);
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

    internal sealed class EmailRequest
    {
        public string? Email { get; init; }
    }

    internal sealed class RegisterRequest
    {
        public string? Email { get; init; }

        public string? GivenName { get; init; }

        public string? Surname { get; init; }

        /// <summary>A field no person fills in. A bot does, and gets the normal answer and nothing else.</summary>
        public string? Website { get; init; }

        /// <summary>The app's profile. The app's validator decides which of its fields are required.</summary>
        public TProfile Profile { get; init; } = new();
    }

    internal sealed class VerifyRequest
    {
        public string? Email { get; init; }

        public string? Code { get; init; }
    }

    internal sealed class ProfileRequest
    {
        public string? GivenName { get; init; }

        public string? MiddleName { get; init; }

        public string? Surname { get; init; }

        public TProfile Profile { get; init; } = new();

        public ProfileChanges<TProfile> ToChanges()
        {
            return new ProfileChanges<TProfile>
            {
                GivenName = GivenName ?? string.Empty,
                MiddleName = MiddleName,
                Surname = Surname ?? string.Empty,
                Profile = Profile,
            };
        }
    }

    internal sealed class ProfileResponse
    {
        public int Id { get; init; }

        public string? Email { get; init; }

        public string? Name { get; init; }

        public string? DisplayName { get; init; }

        public string? GivenName { get; init; }

        public string? MiddleName { get; init; }

        public string? Surname { get; init; }

        public string[] Roles { get; init; } = [];

        public TProfile Profile { get; init; } = new();
    }
}

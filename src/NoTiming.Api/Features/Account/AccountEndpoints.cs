using Microsoft.AspNetCore.Identity;
using MongoDB.Bson;
using Not.Identity;
using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.Account;

/// <summary>
/// Signing in and out, and who is signed in (ADR-0002). The routes follow the rest-api skill: a code request and a
/// session are resources that are created, the current session is deleted.
/// </summary>
internal static class AccountEndpoints
{
    const string CODE_CHALLENGES = "code-challenges";
    const string SESSIONS = "sessions";
    const string ACCOUNTS = "accounts";

    public static IEndpointRouteBuilder MapAccount(this IEndpointRouteBuilder app)
    {
        // The sign-in surface has to be anonymous: it is how anyone becomes signed in.
        app.MapGet("/sign-in", AccountPages.SignIn).AllowAnonymous();
        app.MapGet("/account/assets/{name}", AccountPages.Asset).AllowAnonymous();
        app.MapPost("/api/code-challenges", RequestCode).AllowAnonymous();
        app.MapPost("/api/sessions", CreateSession).AllowAnonymous();
        app.MapDelete("/api/sessions/current", DeleteSession).AllowAnonymous();

        app.MapGet("/api/me", GetMe).RequireAuthorization();
        return app;
    }

    static async Task<IResult> RequestCode(HttpContext context, CodeSignIn codeSignIn, AccountText text)
    {
        var read = await JsonApiRequests.ReadAsync<CodeChallengeAttributes>(context.Request, CODE_CHALLENGES);
        if (read.Error != null)
        {
            return read.Error;
        }

        var email = read.Attributes!.Email;
        if (!CodeSignIn.IsValidEmail(email))
        {
            return JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "invalid-email",
                "Enter a valid email address."
            );
        }

        await codeSignIn.RequestAsync(email!, text.Resolve(context.Request), context.RequestAborted);

        // 202 for an address with an account, without one and cooling down alike: the answer tells nothing.
        return Results.Accepted();
    }

    static async Task<IResult> CreateSession(HttpContext context, CodeSignIn codeSignIn)
    {
        var read = await JsonApiRequests.ReadAsync<SessionAttributes>(context.Request, SESSIONS);
        if (read.Error != null)
        {
            return read.Error;
        }

        var attributes = read.Attributes!;
        if (!CodeSignIn.IsValidEmail(attributes.Email) || string.IsNullOrWhiteSpace(attributes.Code))
        {
            return InvalidCode();
        }

        var user = await codeSignIn.VerifyAsync(attributes.Email!, attributes.Code, context.RequestAborted);
        return user is null
            ? InvalidCode()
            : JsonApiResults.Resource(
                StatusCodes.Status201Created,
                SESSIONS,
                "current",
                new { accountId = user.Id, email = user.Email },
                "/api/me"
            );
    }

    static async Task<IResult> DeleteSession(SignInManager<NIdentityUser> signIn)
    {
        // Deletes the ticket the cookie names and clears the cookie. Without a session there is nothing to delete.
        await signIn.SignOutAsync();
        return Results.NoContent();
    }

    static async Task<IResult> GetMe(HttpContext context, UserManager<NIdentityUser> users)
    {
        var user = await users.GetUserAsync(context.User);
        if (user is null)
        {
            return JsonApiResults.Error(StatusCodes.Status401Unauthorized, "not-signed-in", "Sign in to do this.");
        }

        return JsonApiResults.Resource(
            StatusCodes.Status200OK,
            ACCOUNTS,
            user.Id.ToString(),
            new
            {
                email = user.Email,
                emailConfirmed = user.EmailConfirmed,
                name = TextOf(user, "Name"),
            }
        );
    }

    static IResult InvalidCode()
    {
        return JsonApiResults.Error(
            StatusCodes.Status401Unauthorized,
            "invalid-code",
            "The code is wrong or has expired."
        );
    }

    /// <summary>A field of the application's own user document, which identity does not know.</summary>
    static string? TextOf(NIdentityUser user, string field)
    {
        return
            user.OtherFields != null
            && user.OtherFields.TryGetValue(field, out var value)
            && value.BsonType == BsonType.String
            && !string.IsNullOrWhiteSpace(value.AsString)
            ? value.AsString
            : null;
    }
}

internal sealed class CodeChallengeAttributes
{
    public string? Email { get; set; }
}

internal sealed class SessionAttributes
{
    public string? Email { get; set; }
    public string? Code { get; set; }
}

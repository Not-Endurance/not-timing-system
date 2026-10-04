using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using MongoDB.Bson;
using Not.Identity;
using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.Account;

/// <summary>
/// Signing in and out, and who is signed in (ADR-0002). The routes follow the rest-api skill: a code request and a
/// session are resources that are created, the current session is deleted. A session is created with a code that was
/// mailed or with a passkey credential.
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
        app.MapRegistration();

        // The page looks at the session itself and sends a visitor to sign in, so it is not an API route.
        app.MapGet("/account/passkeys", AccountPages.Passkeys).AllowAnonymous();
        app.MapPasskeys();

        app.MapGet("/api/me", GetMe).RequireAuthorization();
        return app;
    }

    static async Task<IResult> RequestCode(
        HttpContext context,
        CodeSignIn codeSignIn,
        AuthRateLimiter limits,
        AccountText text
    )
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

        // Asking costs the same whether or not the address has an account, so a refusal tells nothing about that.
        if (limits.TrySend(email!, ClientAddress.Of(context)) is { } refusal)
        {
            return JsonApiResults.RateLimited(refusal.RetryAfter);
        }

        await codeSignIn.RequestAsync(email!, text.Resolve(context.Request), context.RequestAborted);

        // 202 for an address with an account, without one and cooling down alike: the answer tells nothing.
        return Results.Accepted();
    }

    static async Task<IResult> CreateSession(
        HttpContext context,
        CodeSignIn codeSignIn,
        AuthRateLimiter limits,
        PasskeyService passkeys,
        IAntiforgery antiforgery,
        UserManager<NIdentityUser> users
    )
    {
        var read = await JsonApiRequests.ReadAsync<SessionAttributes>(context.Request, SESSIONS);
        if (read.Error != null)
        {
            return read.Error;
        }

        var attributes = read.Attributes!;
        if (attributes.Credential is { ValueKind: JsonValueKind.Object } credential)
        {
            if (!passkeys.Availability.Enabled)
            {
                return PasskeyEndpoints.NotConfigured();
            }

            if (await PasskeyEndpoints.ValidateAntiforgeryAsync(context, antiforgery) is { } forged)
            {
                return forged;
            }

            var holder = await passkeys.SignInAsync(credential.GetRawText());
            return holder is null
                ? JsonApiResults.Error(
                    StatusCodes.Status401Unauthorized,
                    "invalid-passkey",
                    "The passkey could not sign you in."
                )
                : await SessionOf(holder, "passkey", users);
        }

        if (!CodeSignIn.IsValidEmail(attributes.Email) || string.IsNullOrWhiteSpace(attributes.Code))
        {
            return InvalidCode();
        }

        // An attempt costs the budgets of a wrong code before the code is looked at, and a right one gets it back.
        var client = ClientAddress.Of(context);
        if (limits.TryVerify(attributes.Email!, client) is { } refusal)
        {
            return JsonApiResults.RateLimited(refusal.RetryAfter);
        }

        var user = await codeSignIn.VerifyAsync(
            attributes.Email!,
            attributes.Code,
            context.Request.Cookies[RegistrationTokens.COOKIE],
            context.RequestAborted
        );
        if (user is null)
        {
            return InvalidCode();
        }

        limits.Succeeded(attributes.Email!, client);

        // Whatever registration the browser had is over: the address is proved.
        context.Response.Cookies.Delete(RegistrationTokens.COOKIE, RegistrationTokens.Options());
        return await SessionOf(user, "code", users);
    }

    static async Task<IResult> DeleteSession(
        HttpContext context,
        SignInManager<NIdentityUser> signIn,
        UserManager<NIdentityUser> users,
        ILoggerFactory loggers
    )
    {
        // Deletes the ticket the cookie names and clears the cookie. Without a session there is nothing to delete.
        var userId = users.GetUserId(context.User);
        await signIn.SignOutAsync();
        if (userId != null)
        {
            loggers
                .CreateLogger(typeof(AccountEndpoints))
                .LogInformation(AuthEvents.SIGNED_OUT, "User {UserId} signed out.", userId);
        }

        return Results.NoContent();
    }

    static async Task<IResult> GetMe(HttpContext context, UserManager<NIdentityUser> users)
    {
        var user = await users.GetUserAsync(context.User);
        if (user is null)
        {
            return JsonApiResults.NotSignedIn();
        }

        return JsonApiResults.Resource(
            StatusCodes.Status200OK,
            ACCOUNTS,
            user.Id.ToString(),
            new
            {
                email = user.Email,
                emailConfirmed = user.EmailConfirmed,
                name = user.TextOf("Name"),
                passkeys = user.Passkeys.Count,
            }
        );
    }

    /// <summary>
    /// The session that was just created. It says how the person signed in and how many passkeys they have, which is
    /// what the sign-in page needs to decide whether to offer one (after a code, until there are two).
    /// </summary>
    static async Task<IResult> SessionOf(NIdentityUser user, string method, UserManager<NIdentityUser> users)
    {
        return JsonApiResults.Resource(
            StatusCodes.Status201Created,
            SESSIONS,
            "current",
            new
            {
                accountId = user.Id,
                email = user.Email,
                method,
                passkeyCount = (await users.GetPasskeysAsync(user)).Count,
            },
            "/api/me"
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
}

internal sealed class CodeChallengeAttributes
{
    public string? Email { get; set; }
}

internal sealed class SessionAttributes
{
    public string? Email { get; set; }
    public string? Code { get; set; }

    /// <summary>The assertion a passkey made, as the JSON of the browser's WebAuthn API: the other way to sign in.</summary>
    public JsonElement? Credential { get; set; }
}

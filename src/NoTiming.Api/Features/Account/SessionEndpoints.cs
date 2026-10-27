using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Not.Identity;
using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.Account;

/// <summary>
/// Signing in and out (ADR-0002). A session is a resource that is created, with a code that was mailed or with a passkey
/// credential, and the current session is deleted.
/// </summary>
internal static class SessionEndpoints
{
    const string SESSIONS = "sessions";

    public static IEndpointRouteBuilder MapSessions(this IEndpointRouteBuilder app)
    {
        // Anonymous because it is how anyone becomes signed in: public because the list of public endpoints names it.
        app.MapPost("/api/sessions", PostSessions);
        app.MapDelete("/api/sessions/current", DeleteSessionsCurrent);
        return app;
    }

    static async Task<IResult> PostSessions(
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
            if (await PasskeyCeremony.RefuseAsync(context, passkeys, antiforgery) is { } forged)
            {
                return forged;
            }

            var holder = await passkeys.SignInAsync(credential.GetRawText());
            return holder is null
                ? AccountFailures.SessionInvalidPasskey.AsResult()
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

    static async Task<IResult> DeleteSessionsCurrent(
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
                .CreateLogger(typeof(SessionEndpoints))
                .LogInformation(AuthEvents.SIGNED_OUT, "User {UserId} signed out.", userId);
        }

        return Results.NoContent();
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
        return AccountFailures.InvalidCode.AsResult();
    }
}

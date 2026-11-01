using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.Account;

/// <summary>
/// Asking for a code to sign in with (ADR-0002): a code challenge is a resource that is created, and the answer is the same
/// whether or not the address has an account.
/// </summary>
internal static class CodeChallengeEndpoints
{
    const string CODE_CHALLENGES = "code-challenges";

    public static IEndpointRouteBuilder MapCodeChallenges(this IEndpointRouteBuilder app)
    {
        // Anonymous because it is how anyone becomes signed in: public because the list of public endpoints names it.
        app.MapPost("/api/code-challenges", PostCodeChallenges);
        return app;
    }

    static async Task<IResult> PostCodeChallenges(
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
            return AccountFailures.InvalidEmail.AsResult();
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
}

using Microsoft.AspNetCore.Identity;
using Not.Identity;
using NoTiming.Api.Features.Access;
using NoTiming.Api.JsonApi;
using NTS.Domain.Access;

namespace NoTiming.Api.Features.Tenancy;

/// <summary>
/// The search of accounts by name (#643, ADR-0012), which is how a Tenant Root or a Main Operator finds the person to link
/// or to assign. <c>GET /api/accounts</c> looks among the members of the Tenant the account acts in, and
/// <c>GET /api/accounts/all-tenants</c> is the named view that reaches across Tenants for the people who may search at all:
/// no parameter turns the first into the second. Both take <c>filter=contains(name,'text')</c> and nothing else, with at
/// least three characters, and answer at most ten matches. A request is answered in this order: who is asking, whether they
/// may, what they asked, and whether they have searched too often.
/// </summary>
internal static class AccountSearchEndpoints
{
    const string ACCOUNT_MATCHES = "account-matches";
    const string NAME = "name";

    public static IEndpointRouteBuilder MapAccountSearch(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/accounts", SearchInTheTenant).RequireAuthorization();
        app.MapGet("/api/accounts/all-tenants", SearchEveryTenant).RequireAuthorization();
        return app;
    }

    static async Task<IResult> SearchInTheTenant(
        HttpContext context,
        UserManager<NIdentityUser> users,
        CallerReader callers,
        TenantStore tenants,
        AccountSearch search,
        SearchRateLimiter limiter
    )
    {
        var user = await users.GetUserAsync(context.User);
        if (user is null)
        {
            return JsonApiResults.NotSignedIn();
        }

        var caller = await callers.ReadAsync(user, context.RequestAborted);
        var tenantId = AccountRoles.CurrentTenantOf(user);

        // With no Tenant there is nobody to find, but whether the caller may search is still told as it is anywhere.
        var verdict =
            tenantId == null
                ? AccessPolicy.Decide(Capability.SearchAccountsAcrossTenants, caller, AccessScope.Platform)
                : AccessPolicy.Decide(
                    Capability.SearchAccounts,
                    caller,
                    AccessScope.ForTenant(tenantId, await tenants.IsOperationalAsync(tenantId, context.RequestAborted))
                );
        return await AnswerAsync(context, user, verdict, tenantId, search, limiter);
    }

    static async Task<IResult> SearchEveryTenant(
        HttpContext context,
        UserManager<NIdentityUser> users,
        CallerReader callers,
        AccountSearch search,
        SearchRateLimiter limiter
    )
    {
        var user = await users.GetUserAsync(context.User);
        if (user is null)
        {
            return JsonApiResults.NotSignedIn();
        }

        var caller = await callers.ReadAsync(user, context.RequestAborted);
        var verdict = AccessPolicy.Decide(Capability.SearchAccountsAcrossTenants, caller, AccessScope.Platform);
        return await AnswerAsync(context, user, verdict, null, search, limiter, acrossTenants: true);
    }

    static async Task<IResult> AnswerAsync(
        HttpContext context,
        NIdentityUser user,
        Verdict verdict,
        string? tenantId,
        AccountSearch search,
        SearchRateLimiter limiter,
        bool acrossTenants = false
    )
    {
        if (!verdict.IsAllowed)
        {
            return AccessResults.Refused(verdict);
        }

        var query = JsonApiQuery.ReadSearch(context.Request, NAME);
        if (query.Refusal != null)
        {
            return query.Refusal;
        }

        if (query.Text!.Length < AccountSearch.MIN_SEARCH_LENGTH)
        {
            return JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "search-too-short",
                $"Search for at least {AccountSearch.MIN_SEARCH_LENGTH} characters of a name."
            );
        }

        if (limiter.TrySearch(user.Id) is { } refusal)
        {
            return JsonApiResults.RateLimited(refusal.RetryAfter);
        }

        // An account that acts in no Tenant finds nobody in it; across Tenants it finds whoever is there.
        var found =
            tenantId == null && !acrossTenants
                ? []
                : await search.SearchAsync(query.Text, tenantId, context.RequestAborted);
        return JsonApiResults.Collection(
            ACCOUNT_MATCHES,
            found.Select(x => (x.Id.ToString(), (object)new { displayName = x.DisplayName, email = x.MaskedEmail }))
        );
    }
}

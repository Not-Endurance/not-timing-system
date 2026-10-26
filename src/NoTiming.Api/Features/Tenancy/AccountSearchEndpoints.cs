using NoTiming.Api.Features.Access;
using NoTiming.Api.JsonApi;
using NTS.Domain.Access;

namespace NoTiming.Api.Features.Tenancy;

/// <summary>
/// The search of accounts by name (#643, ADR-0012), which is how Root or a Console Operator finds the person to link or to
/// assign. <c>GET /api/accounts</c> looks among the members of the Tenant the account acts in, and
/// <c>GET /api/accounts/all-tenants</c> is the named view that reaches across Tenants for the people who may search at all:
/// no parameter turns the first into the second. Both take <c>filter=contains(name,'text')</c>, whose words have to be parts of
/// the name in any order and in any case, with one of at least two characters, the <c>sort</c> of the names and the pages
/// of every list (ten matches a page, twenty-five at most), and nothing else. A request is answered in this order: the query,
/// who is asking, whether they may, the length of the words, and whether they have searched too often, a page being a search.
/// </summary>
internal static class AccountSearchEndpoints
{
    const string ACCOUNT_MATCHES = "account-matches";

    public static IEndpointRouteBuilder MapAccountSearch(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/accounts", SearchInTheTenant);
        app.MapGet("/api/accounts/all-tenants", SearchEveryTenant);
        return app;
    }

    // With no Tenant there is nobody to find, but whether the caller may search is still told as it is anywhere.
    [Requires(Capability.EditRegistry, PlaceIn.CurrentTenant, WithoutTenant = Capability.SetUpAcrossTenants)]
    static Task<IResult> SearchInTheTenant(
        SearchQuery query,
        HttpRequest request,
        AccessContext access,
        AccountSearch search,
        SearchRateLimiter limiter,
        CancellationToken cancellationToken
    )
    {
        return AnswerAsync(
            query.Search,
            request,
            access,
            access.HasTenant ? access.Tenant.Id : null,
            search,
            limiter,
            acrossTenants: false,
            cancellationToken
        );
    }

    [Requires(Capability.SetUpAcrossTenants, PlaceIn.Platform)]
    static Task<IResult> SearchEveryTenant(
        SearchQuery query,
        HttpRequest request,
        AccessContext access,
        AccountSearch search,
        SearchRateLimiter limiter,
        CancellationToken cancellationToken
    )
    {
        return AnswerAsync(
            query.Search,
            request,
            access,
            null,
            search,
            limiter,
            acrossTenants: true,
            cancellationToken
        );
    }

    static async Task<IResult> AnswerAsync(
        JsonApiSearch query,
        HttpRequest request,
        AccessContext access,
        string? tenantId,
        AccountSearch search,
        SearchRateLimiter limiter,
        bool acrossTenants,
        CancellationToken cancellationToken
    )
    {
        if (limiter.TrySearch(access.User.Id) is { } refusal)
        {
            return JsonApiResults.RateLimited(refusal.RetryAfter);
        }

        // An account that acts in no Tenant finds nobody in it; across Tenants it finds whoever is there.
        var found =
            tenantId == null && !acrossTenants
                ? []
                : await search.SearchAsync(NameSearch.Of(query), tenantId, cancellationToken);
        return JsonApiResults.Collection(
            ACCOUNT_MATCHES,
            found
                .Take(query.Size)
                .Select(x => (x.Id.ToString(), (object)new { displayName = x.DisplayName, email = x.MaskedEmail })),
            links: JsonApiPaging.Links(request, query.Number, found.Count > query.Size),
            meta: JsonApiPaging.Meta(query.Size, query.Number)
        );
    }
}

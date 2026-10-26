using NoTiming.Api.Features.Access;
using NoTiming.Api.JsonApi;
using NTS.Domain.Access;

namespace NoTiming.Api.Features.Tenancy;

/// <summary>The search of one registry over the rows of every Tenant: a named capability of <see cref="CrossTenantReads"/>.</summary>
internal delegate Task<IReadOnlyList<RegistryMatch>> RegistrySearch(
    Standing standing,
    NameSearch search,
    CancellationToken cancellationToken
);

/// <summary>
/// The registries of Athletes, Horses, Clubs and Officials searched across Tenants (#643, ADR-0012, ADR-0010), each through
/// a named view of its own: <c>GET /api/athletes/all-tenants</c> and the like. A row is owned by the Tenant that registered
/// it and is found by another through the cross-tenant search, which every signed-in account may make. A search takes
/// <c>filter=contains(name,'text')</c>, where the name stands for what names a row: its name, its English name and, for
/// Athletes and Horses, its FEI ID. Every word of the text has to be part of one of those, whatever the case and in any
/// order, and one of the words has to be of two characters at least (<see cref="NameSearch"/>). The names are in order, or
/// in the order the other way round (<c>sort</c>), and a list is paged as every list is, ten rows to a page and twenty-five at
/// most. It answers only the fields that are meant to leave, and draws on the budget of searches that the search of accounts
/// draws on, a page being a search. The database does all of it. The row of an Athlete or a Horse is its name and its name in
/// English to every signed-in account, and its whole row (FEI ID, club, country, the Tenant) to the people who set an Event up
/// (<see cref="Capability.SetUpAcrossTenants"/>), who are also the only ones whose words are matched against the FEI
/// ID. The Tenant's own list of a registry is a route of the registry's family, and no parameter turns it into this one. What
/// a request is asked for is the endpoint filter's (the query, then the caller, then the length of the words); the handler
/// draws on the budget and searches.
/// </summary>
internal static class RegistrySearchEndpoints
{
    public static IEndpointRouteBuilder MapRegistrySearch(this IEndpointRouteBuilder app)
    {
        MapSearch(app, "athletes", reads => reads.SearchAthletesAsync);
        MapSearch(app, "horses", reads => reads.SearchHorsesAsync);
        MapSearch(app, "clubs", reads => reads.SearchClubsAsync);
        MapSearch(app, "officials", reads => reads.SearchOfficialsAsync);
        return app;
    }

    /// <summary>The named view <c>/api/{type}/all-tenants</c> of a registry, over the search of that registry.</summary>
    static void MapSearch(IEndpointRouteBuilder app, string type, Func<CrossTenantReads, RegistrySearch> searchOf)
    {
        app.MapGet(
            $"/api/{type}/all-tenants",
            [Requires(Capability.SignedIn, PlaceIn.Platform)]
            (
                SearchQuery query,
                HttpRequest request,
                AccessContext access,
                CrossTenantReads reads,
                SearchRateLimiter limiter,
                CancellationToken cancellationToken
            ) => SearchAsync(query.Search, request, access, limiter, type, searchOf(reads), cancellationToken)
        );
    }

    static async Task<IResult> SearchAsync(
        JsonApiSearch query,
        HttpRequest request,
        AccessContext access,
        SearchRateLimiter limiter,
        string type,
        RegistrySearch search,
        CancellationToken cancellationToken
    )
    {
        if (limiter.TrySearch(access.User.Id) is { } refusal)
        {
            return JsonApiResults.RateLimited(refusal.RetryAfter);
        }

        var found = await search(access.Standing, NameSearch.Of(query), cancellationToken);
        return JsonApiResults.Collection(
            type,
            found
                .Take(query.Size)
                .Select(x =>
                    (
                        x.Id.ToString(),
                        x.IsNameOnly
                            ? (object)new { name = x.Name, nameEnglish = x.NameEnglish }
                            : new
                            {
                                tenantId = x.TenantId,
                                name = x.Name,
                                nameEnglish = x.NameEnglish,
                                feiId = x.FeiId,
                                club = x.Club,
                                country = x.Country,
                                role = x.Role,
                            }
                    )
                ),
            links: JsonApiPaging.Links(request, query.Number, found.Count > query.Size),
            meta: JsonApiPaging.Meta(query.Size, query.Number)
        );
    }
}

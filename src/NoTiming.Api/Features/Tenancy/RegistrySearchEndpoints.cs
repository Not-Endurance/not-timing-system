using Microsoft.AspNetCore.Identity;
using Not.Identity;
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
/// ID. The Tenant's own list of a registry is a route of the registry's family, and no parameter turns it into this one.
/// </summary>
internal static class RegistrySearchEndpoints
{
    const string NAME = "name";

    public static IEndpointRouteBuilder MapRegistrySearch(this IEndpointRouteBuilder app)
    {
        MapSearch(app, "athletes", reads => reads.SearchAthletesAsync);
        MapSearch(app, "horses", reads => reads.SearchHorsesAsync);
        MapSearch(app, "clubs", reads => reads.SearchClubsAsync);
        MapSearch(app, "officials", reads => reads.SearchOfficialsAsync);
        return app;
    }

    /// <summary>
    /// What every search of a registry asks before it reads (the rest-api skill): the caller is signed in and may read the
    /// registries, the request is a search of the form <c>contains(name,'text')</c> with its sort and its page and nothing
    /// else, one of the words of the text is long enough, and there is room for it in the budget of searches. Where the caller
    /// stands and the search are the answer, or the answer that refuses.
    /// </summary>
    public static async Task<SearchAdmission> AdmitAsync(
        HttpContext context,
        UserManager<NIdentityUser> users,
        StandingReader standings,
        SearchRateLimiter limiter
    )
    {
        var user = await users.GetUserAsync(context.User);
        if (user is null)
        {
            return SearchAdmission.Refused(JsonApiResults.NotSignedIn());
        }

        var standing = await standings.AtPlatformAsync(user, context.RequestAborted);
        var verdict = AccessPolicy.Decide(Capability.SignedIn, standing, PlaceState.Platform);
        if (!verdict.IsAllowed)
        {
            return SearchAdmission.Refused(AccessResults.Refused(verdict));
        }

        var query = JsonApiQuery.ReadSearch(context.Request, NAME);
        if (query.Refusal != null)
        {
            return SearchAdmission.Refused(query.Refusal);
        }

        if (!NameSearch.HasLongWord(query.Words!))
        {
            return SearchAdmission.Refused(TooShort());
        }

        if (limiter.TrySearch(user.Id) is { } refusal)
        {
            return SearchAdmission.Refused(JsonApiResults.RateLimited(refusal.RetryAfter));
        }

        return SearchAdmission.Admitted(standing, query);
    }

    /// <summary>400 <c>search-too-short</c>: none of the words of the text has the characters that make it a search and not a listing.</summary>
    public static IResult TooShort()
    {
        return JsonApiResults.Error(
            StatusCodes.Status400BadRequest,
            "search-too-short",
            $"Search with a word of at least {NameSearch.MIN_WORD_LENGTH} characters."
        );
    }

    /// <summary>The named view <c>/api/{type}/all-tenants</c> of a registry, over the search of that registry.</summary>
    static void MapSearch(IEndpointRouteBuilder app, string type, Func<CrossTenantReads, RegistrySearch> searchOf)
    {
        app.MapGet(
                $"/api/{type}/all-tenants",
                (
                    HttpContext context,
                    UserManager<NIdentityUser> users,
                    StandingReader standings,
                    CrossTenantReads reads,
                    SearchRateLimiter limiter
                ) => Search(context, users, standings, limiter, type, searchOf(reads))
            )
            .RequireAuthorization();
    }

    static async Task<IResult> Search(
        HttpContext context,
        UserManager<NIdentityUser> users,
        StandingReader standings,
        SearchRateLimiter limiter,
        string type,
        RegistrySearch search
    )
    {
        var admission = await AdmitAsync(context, users, standings, limiter);
        if (admission.Refusal != null)
        {
            return admission.Refusal;
        }

        var query = admission.Query!;
        var found = await search(admission.Standing!, NameSearch.Of(query), context.RequestAborted);
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
            links: JsonApiPaging.Links(context.Request, query.Number, found.Count > query.Size),
            meta: JsonApiPaging.Meta(query.Size, query.Number)
        );
    }
}

/// <summary>A search of a registry that may go on, with where the caller stands and what the search asks for, or the answer that refuses it.</summary>
internal sealed class SearchAdmission
{
    public static SearchAdmission Admitted(Standing standing, JsonApiSearch query)
    {
        return new SearchAdmission(standing, query, null);
    }

    public static SearchAdmission Refused(IResult refusal)
    {
        return new SearchAdmission(null, null, refusal);
    }

    SearchAdmission(Standing? standing, JsonApiSearch? query, IResult? refusal)
    {
        Standing = standing;
        Query = query;
        Refusal = refusal;
    }

    public Standing? Standing { get; }
    public JsonApiSearch? Query { get; }
    public IResult? Refusal { get; }
}

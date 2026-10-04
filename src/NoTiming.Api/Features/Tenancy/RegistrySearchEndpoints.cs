using Microsoft.AspNetCore.Identity;
using Not.Identity;
using NoTiming.Api.Features.Access;
using NoTiming.Api.JsonApi;
using NTS.Domain.Access;

namespace NoTiming.Api.Features.Tenancy;

/// <summary>The search of one registry over the rows of every Tenant: a named capability of <see cref="CrossTenantReads"/>.</summary>
internal delegate Task<IReadOnlyList<RegistryMatch>> RegistrySearch(
    Caller caller,
    string text,
    CancellationToken cancellationToken
);

/// <summary>
/// The registries of Athletes, Horses, Clubs and Officials searched across Tenants (#643, ADR-0012, ADR-0010), each through
/// a named view of its own: <c>GET /api/athletes/all-tenants</c> and the like. A row is owned by the Tenant that registered
/// it and is found by another through the cross-tenant search, which every signed-in account may make. A search takes
/// <c>filter=contains(name,'text')</c>, where the name stands for what names a row: its name, its English name and, for
/// Athletes and Horses, its FEI ID. It takes at least three characters, matches them as written, answers at most ten rows
/// with only the fields that are meant to leave, and draws on the budget of searches that the search of accounts draws on.
/// The Tenant's own list of a registry is a route of the registry's family, and no parameter turns it into this one.
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

    /// <summary>The named view <c>/api/{type}/all-tenants</c> of a registry, over the search of that registry.</summary>
    static void MapSearch(IEndpointRouteBuilder app, string type, Func<CrossTenantReads, RegistrySearch> searchOf)
    {
        app.MapGet(
                $"/api/{type}/all-tenants",
                (
                    HttpContext context,
                    UserManager<NIdentityUser> users,
                    CrossTenantReads reads,
                    SearchRateLimiter limiter
                ) => Search(context, users, limiter, type, searchOf(reads))
            )
            .RequireAuthorization();
    }

    static async Task<IResult> Search(
        HttpContext context,
        UserManager<NIdentityUser> users,
        SearchRateLimiter limiter,
        string type,
        RegistrySearch search
    )
    {
        var user = await users.GetUserAsync(context.User);
        if (user is null)
        {
            return JsonApiResults.NotSignedIn();
        }

        var caller = AccountRoles.CallerOf(user);
        var verdict = AccessPolicy.Decide(Capability.ReadRegistryAcrossTenants, caller, AccessScope.Platform);
        if (!verdict.IsAllowed)
        {
            return AccessResults.Refused(verdict);
        }

        var query = JsonApiQuery.ReadSearch(context.Request, NAME);
        if (query.Refusal != null)
        {
            return query.Refusal;
        }

        if (query.Text!.Length < CrossTenantReads.MIN_SEARCH_LENGTH)
        {
            return JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "search-too-short",
                $"Search for at least {CrossTenantReads.MIN_SEARCH_LENGTH} characters."
            );
        }

        if (limiter.TrySearch(user.Id) is { } refusal)
        {
            return JsonApiResults.RateLimited(refusal.RetryAfter);
        }

        var found = await search(caller, query.Text, context.RequestAborted);
        return JsonApiResults.Collection(
            type,
            found.Select(x =>
                (
                    x.Id.ToString(),
                    (object)
                        new
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
            )
        );
    }
}

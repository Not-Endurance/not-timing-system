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

    /// <summary>
    /// What every search of a registry asks before it reads (the rest-api skill): the caller is signed in and may read the
    /// registries, the request is a search of the form <c>contains(name,'text')</c> and nothing else, the text is long enough,
    /// and there is room for it in the budget of searches. The caller and the text are the answer, or the answer that refuses.
    /// </summary>
    public static async Task<SearchAdmission> AdmitAsync(
        HttpContext context,
        UserManager<NIdentityUser> users,
        SearchRateLimiter limiter
    )
    {
        var user = await users.GetUserAsync(context.User);
        if (user is null)
        {
            return SearchAdmission.Refused(JsonApiResults.NotSignedIn());
        }

        var caller = AccountRoles.CallerOf(user);
        var verdict = AccessPolicy.Decide(Capability.ReadRegistryAcrossTenants, caller, AccessScope.Platform);
        if (!verdict.IsAllowed)
        {
            return SearchAdmission.Refused(AccessResults.Refused(verdict));
        }

        var query = JsonApiQuery.ReadSearch(context.Request, NAME);
        if (query.Refusal != null)
        {
            return SearchAdmission.Refused(query.Refusal);
        }

        if (query.Text!.Length < CrossTenantReads.MIN_SEARCH_LENGTH)
        {
            return SearchAdmission.Refused(
                JsonApiResults.Error(
                    StatusCodes.Status400BadRequest,
                    "search-too-short",
                    $"Search for at least {CrossTenantReads.MIN_SEARCH_LENGTH} characters."
                )
            );
        }

        if (limiter.TrySearch(user.Id) is { } refusal)
        {
            return SearchAdmission.Refused(JsonApiResults.RateLimited(refusal.RetryAfter));
        }

        return SearchAdmission.Admitted(caller, query.Text);
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
        var admission = await AdmitAsync(context, users, limiter);
        if (admission.Refusal != null)
        {
            return admission.Refusal;
        }

        var found = await search(admission.Caller!, admission.Text!, context.RequestAborted);
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

/// <summary>A search of a registry that may go on, with the caller and the text to look for, or the answer that refuses it.</summary>
internal sealed class SearchAdmission
{
    public static SearchAdmission Admitted(Caller caller, string text)
    {
        return new SearchAdmission(caller, text, null);
    }

    public static SearchAdmission Refused(IResult refusal)
    {
        return new SearchAdmission(null, null, refusal);
    }

    SearchAdmission(Caller? caller, string? text, IResult? refusal)
    {
        Caller = caller;
        Text = text;
        Refusal = refusal;
    }

    public Caller? Caller { get; }
    public string? Text { get; }
    public IResult? Refusal { get; }
}

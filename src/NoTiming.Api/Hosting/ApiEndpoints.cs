using NoTiming.Api.Features.Access;
using NoTiming.Api.Features.Account;
using NoTiming.Api.Features.Branding;
using NoTiming.Api.Features.EventData;
using NoTiming.Api.Features.Events;
using NoTiming.Api.Features.Live;
using NoTiming.Api.Features.Localhost;
using NoTiming.Api.Features.Participants;
using NoTiming.Api.Features.Profile;
using NoTiming.Api.Features.Reference;
using NoTiming.Api.Features.Snapshots;
using NoTiming.Api.Features.Tenancy;
using NoTiming.Api.Features.UserSessions;
using NTS.Contracts;

namespace NoTiming.Api.Hosting;

/// <summary>
/// Every endpoint of the host, in the order that matters: what a feature maps, the live hub, the answer to an unknown API
/// route and the deep links into the Ui, which come last because they match what is left. The middleware is
/// <see cref="ApiPipeline"/>'s, and what a feature maps is its own.
/// </summary>
internal static class ApiEndpoints
{
    public static WebApplication MapNoTimingApi(this WebApplication app)
    {
        app.MapHealth();

        // The routes that declare what they need are mapped through the group the endpoint filter is on (ADR-0012, #671).
        var guarded = app.MapGroup(string.Empty).AddEndpointFilter<AccessFilter>();
        guarded.MapAccountPages();
        guarded.MapCodeChallenges();
        guarded.MapSessions();
        guarded.MapRegistrations();
        guarded.MapPasskeys();
        guarded.MapMe();
app.MapLocalhost();
        guarded.MapProfile();
        guarded.MapTenants();
        guarded.MapBranding();
        guarded.MapEvents();
        app.MapHistoricEventRedirects();
        guarded.MapSetups();
        guarded.MapEventGrants();
        guarded.MapConsoleOperators();
        guarded.MapAccountSearch();
        guarded.MapRegistrySearch();
        guarded.MapParticipationHistory();
        guarded.MapParticipantLinks();
        guarded.MapReference();
        guarded.MapEventData();
        guarded.MapTimeEventActors();
        guarded.MapSnapshots();
        guarded.MapUserSessions();
        app.MapHub<LiveHub>(ApplicationConstants.LIVE_HUB).RequireCors(ApiServices.CORS_POLICY_NAME);
        app.MapUnknownApiRoute();

        // Deep links into the Ui. A path that looks like a file (it has an extension) that is missing stays a 404.
        app.MapFallbackToFile("{**path:nonfile}", "index.html");

        // Fail closed: a route of the Api that is neither public nor declares what it needs does not let the host start.
#if DEBUG
        var hostEntries = app.Services.GetService<LocalhostPublicEndpoints>()?.Entries;
#else
        IEnumerable<PublicEndpoint>? hostEntries = null;
#endif
        EndpointGuard.ThrowWhenUnguarded(
            ((IEndpointRouteBuilder)app).DataSources.SelectMany(x => x.Endpoints),
            hostEntries
        );

        return app;
    }
}

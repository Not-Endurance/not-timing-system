using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Not.Application.Authentication.User;
using Not.Server.Authentication;

namespace NTS.Nexus.Warp.Features.Witness.Authorization;

internal sealed class WitnessReceiveAuthorizer : IWitnessReceiveAuthorizer
{
    readonly IReceiveSnapshotAccessPolicy _officialAccessService;
    readonly string? _requiredScope;

    public WitnessReceiveAuthorizer(
        IReceiveSnapshotAccessPolicy officialAccessService,
        IOptions<NServerAuthenticationSettings> serverAuthenticationOptions
    )
    {
        _officialAccessService = officialAccessService;
        _requiredScope = ResolveScopeName(serverAuthenticationOptions.Value);
    }

    public async Task Authorize(ClaimsPrincipal? principal, string? connectedEventId, string? requestedEventId)
    {
        var connectedEvent = ParseEventId(connectedEventId, "connection");
        var requestedEvent = ParseEventId(requestedEventId, "request");
        if (connectedEvent != requestedEvent)
        {
            throw new HubException("Snapshots can only be sent to the currently connected event.");
        }

        if (principal?.Identity?.IsAuthenticated != true)
        {
            throw new HubException("Authentication is required to send snapshots.");
        }

        if (!NScopeClaimsHelper.HasScope(principal, _requiredScope))
        {
            throw new HubException("The authenticated user is missing the required Warp access scope.");
        }

        var email = NUserClaimsHelper.ResolveEmail(principal);
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new HubException("Authenticated user email is missing.");
        }

        if (!await _officialAccessService.IsOfficial(email, connectedEvent))
        {
            throw new HubException("Only officials can send snapshots for this event.");
        }
    }

    static int ParseEventId(string? enduranceEventId, string source)
    {
        if (!int.TryParse(enduranceEventId, out var eventId))
        {
            throw new HubException($"Witness event ID is missing or invalid for the {source}.");
        }

        return eventId;
    }

    static string? ResolveScopeName(NServerAuthenticationSettings settings)
    {
        var scope = settings.Scope?.Trim();
        if (string.IsNullOrWhiteSpace(scope))
        {
            return null;
        }

        var separatorIndex = scope.LastIndexOf('/');
        return separatorIndex < 0 ? scope : scope[(separatorIndex + 1)..];
    }
}

using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using NC.Application.Authentication.User;

namespace NoTiming.Api.Application.Features.Ui.Authorization;

internal sealed class UiReceiveAuthorizer : IUiReceiveAuthorizer
{
    readonly IReceiveSnapshotAccessPolicy _receiveAccessPolicy;

    public UiReceiveAuthorizer(IReceiveSnapshotAccessPolicy receiveAccessPolicy)
    {
        _receiveAccessPolicy = receiveAccessPolicy;
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

        var email = NUserClaimsHelper.ResolveEmail(principal);
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new HubException("Authenticated user email is missing.");
        }

        if (!await _receiveAccessPolicy.CanReceiveSnapshots(email, connectedEvent))
        {
            throw new HubException("Only authorized event staff can send snapshots for this event.");
        }
    }

    static int ParseEventId(string? eventId, string source)
    {
        if (!int.TryParse(eventId, out var parsedEventId))
        {
            throw new HubException($"Witness event ID is missing or invalid for the {source}.");
        }

        return parsedEventId;
    }
}

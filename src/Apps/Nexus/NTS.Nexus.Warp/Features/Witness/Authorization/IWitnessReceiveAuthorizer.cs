using System.Security.Claims;

namespace NTS.Nexus.Warp.Features.Witness.Authorization;

internal interface IWitnessReceiveAuthorizer
{
    Task Authorize(ClaimsPrincipal? principal, string? connectedEventId, string? requestedEventId);
}

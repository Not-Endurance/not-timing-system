using System.Security.Claims;

namespace NoTiming.Features.Witness.Authorization;

internal interface IWitnessReceiveAuthorizer
{
    Task Authorize(ClaimsPrincipal? principal, string? connectedEventId, string? requestedEventId);
}

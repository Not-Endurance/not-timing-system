using System.Security.Claims;

namespace NoTiming.Api.Application.Features.Witness.Authorization;

public interface IWitnessReceiveAuthorizer
{
    Task Authorize(ClaimsPrincipal? principal, string? connectedEventId, string? requestedEventId);
}

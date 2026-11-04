using System.Security.Claims;

namespace NoTiming.Api.Application.Features.Ui.Authorization;

public interface IUiReceiveAuthorizer
{
    Task Authorize(ClaimsPrincipal? principal, string? connectedEventId, string? requestedEventId);
}

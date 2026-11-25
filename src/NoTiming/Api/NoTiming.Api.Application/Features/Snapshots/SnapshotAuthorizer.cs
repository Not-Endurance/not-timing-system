using System.Security.Claims;
using NC.Application.Authentication.User;
using NC.Domain.Exceptions;
using NoTiming.Api.Application.Features.Ui.Authorization;

namespace NoTiming.Api.Application.Features.Snapshots;

public sealed class SnapshotAuthorizer
{
    readonly IReceiveSnapshotAccessPolicy _receiveAccessPolicy;

    public SnapshotAuthorizer(IReceiveSnapshotAccessPolicy receiveAccessPolicy)
    {
        _receiveAccessPolicy = receiveAccessPolicy;
    }

    public async Task Authorize(ClaimsPrincipal? principal, int eventId)
    {
        if (principal?.Identity?.IsAuthenticated != true)
        {
            throw new DomainException("Authentication is required to send snapshots.");
        }

        var email = NUserClaimsHelper.ResolveEmail(principal);
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainException("Authenticated user email is missing.");
        }

        if (!await _receiveAccessPolicy.CanReceiveSnapshots(email, eventId))
        {
            throw new DomainException("Only authorized event staff can send snapshots for this event.");
        }
    }
}

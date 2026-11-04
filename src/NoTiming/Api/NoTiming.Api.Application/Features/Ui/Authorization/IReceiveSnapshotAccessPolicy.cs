namespace NoTiming.Api.Application.Features.Ui.Authorization;

internal interface IReceiveSnapshotAccessPolicy
{
    Task<bool> CanReceiveSnapshots(string email, int eventId);
}

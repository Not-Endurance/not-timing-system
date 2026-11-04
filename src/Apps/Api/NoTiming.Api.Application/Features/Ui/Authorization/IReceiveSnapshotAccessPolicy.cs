namespace NoTiming.Api.Application.Features.Ui.Authorization;

internal interface IReceiveSnapshotAccessPolicy
{
    Task<bool> CanWriteSnapshots(string email, int eventId);
}

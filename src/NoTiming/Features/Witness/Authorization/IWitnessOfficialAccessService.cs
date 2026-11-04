namespace NoTiming.Features.Witness.Authorization;

internal interface IReceiveSnapshotAccessPolicy
{
    Task<bool> CanWriteSnapshots(string email, int eventId);
}

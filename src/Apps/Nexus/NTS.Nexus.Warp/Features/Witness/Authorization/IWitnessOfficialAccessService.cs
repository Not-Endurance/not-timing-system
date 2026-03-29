namespace NTS.Nexus.Warp.Features.Witness.Authorization;

internal interface IReceiveSnapshotAccessPolicy
{
    Task<bool> IsOfficial(string email, int eventId);
}

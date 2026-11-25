namespace NoTiming.Api.Application.Features.Ui.Authorization;

public interface IReceiveSnapshotAccessPolicy
{
    Task<bool> CanReceiveSnapshots(string email, int eventId);
}

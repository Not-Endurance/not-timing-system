using NTS.Domain.Core.Objects.Snapshots;

namespace NoTiming.Ui.Features.Core.Dashboard;

public interface ISnapshotPublisher
{
    Task PublishSnapshotsAsync(SnapshotGroup snapshoutGroup);
}

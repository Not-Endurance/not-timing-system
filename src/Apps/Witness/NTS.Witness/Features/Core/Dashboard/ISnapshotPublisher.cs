using NTS.Domain.Core.Objects.Snapshots;

namespace NTS.Witness.Features.Core.Dashboard;

public interface ISnapshotPublisher
{
    Task PublishSnapshotsAsync(SnapshotGroup snapshoutGroup);
}

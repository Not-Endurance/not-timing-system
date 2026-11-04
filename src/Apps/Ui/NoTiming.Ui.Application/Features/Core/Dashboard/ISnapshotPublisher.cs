using NTS.Domain.Watcher;

namespace NTS.Witness.Features.Core.Dashboard;

public interface ISnapshotPublisher
{
    Task PublishSnapshotsAsync(SnapshotGroup snapshoutGroup);
}

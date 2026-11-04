using NTS.Domain.Watcher;

namespace NoTiming.Ui.Application.Features.Core.Dashboard;

public interface ISnapshotPublisher
{
    Task PublishSnapshotsAsync(SnapshotGroup snapshoutGroup);
}

using NTS.Application.Watcher;

namespace NTS.Witness.Features.Core.Dashboard;

public interface ISnapshotService
{
    Task PublishSnapshotsAsync(SnapshotModel model);
}

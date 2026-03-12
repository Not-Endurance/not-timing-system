using Not.Application.RPC.Clients;
using NTS.Application.Watcher;
using NTS.Domain.Core.Aggregates;

namespace NTS.Witness.Features.Core.Dashboard;

public interface ISnapshotService
{
    Task<RpcInvokeResult> PublishSnapshotsAsync(SnapshotModel model);
}

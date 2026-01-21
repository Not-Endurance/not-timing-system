using Not.Application.RPC.Clients;
using NTS.Application.Models;
using NTS.Domain.Core.Aggregates;

namespace NTS.Witness.Services;

public interface ISnapshotService
{
    Task<RpcInvokeResult> PublishSnapshotsAsync(SnapshotModel model);
}

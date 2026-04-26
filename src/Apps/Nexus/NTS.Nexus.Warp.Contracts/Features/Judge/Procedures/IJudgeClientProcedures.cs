using NTS.Application.Contracts.Watcher;
using NTS.Application.Contracts.Watcher.Models;

namespace NTS.Nexus.Warp.Contracts.Features.Judge.Procedures;

public interface IJudgeClientProcedures
{
    Task Receive(SnapshotGroupModel snapshotGroup);
}

using NTS.Application.Watcher;

namespace NTS.Nexus.Warp.Contracts.Features.Judge.Procedures;

public interface IJudgeClientProcedures
{
    Task Receive(SnapshotGroupModel snapshotGroup);
}

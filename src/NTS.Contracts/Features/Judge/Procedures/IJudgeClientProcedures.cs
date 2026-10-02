using NTS.Contracts.Watcher;
using NTS.Contracts.Watcher.Models;

namespace NTS.Contracts.Features.Judge.Procedures;

public interface IJudgeClientProcedures
{
    Task Receive(SnapshotGroupModel snapshotGroup);
}

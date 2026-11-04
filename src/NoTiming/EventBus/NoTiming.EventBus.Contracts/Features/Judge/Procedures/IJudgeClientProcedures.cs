using NTS.Application.Contracts.Watcher;
using NTS.Application.Contracts.Watcher.Models;

namespace NoTiming.EventBus.Contracts.Features.Judge.Procedures;

public interface IJudgeClientProcedures
{
    Task Receive(SnapshotGroupModel snapshotGroup);
}

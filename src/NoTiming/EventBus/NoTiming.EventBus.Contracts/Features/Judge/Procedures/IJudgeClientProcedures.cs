using NoTiming.Application.Contracts.Watcher;
using NoTiming.Application.Contracts.Watcher.Models;

namespace NoTiming.EventBus.Contracts.Features.Judge.Procedures;

public interface IJudgeClientProcedures
{
    Task Receive(SnapshotGroupModel snapshotGroup);
}

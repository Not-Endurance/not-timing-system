using NTS.Domain.Aggregates;

namespace NTS.Nexus.Warp.Contracts.Features.Judge.Procedures;

public interface IJudgeClientProcedures
{
    Task Receive(IEnumerable<Snapshot> snapshots);
}

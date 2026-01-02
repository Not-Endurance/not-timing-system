using NTS.Domain.Aggregates;
using NTS.Application.Models;

namespace NTS.Warp.Features.Judge.Procedures;

public interface IParticipationClientProcedures
{
    Task Receive(IEnumerable<Snapshot> snapshots);
    Task<IEnumerable<CoreParticipationModel>> GetActive();
}

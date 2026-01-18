using NTS.Application.Core;
using NTS.Domain.Aggregates;

namespace NTS.Warp.Features.Judge.Procedures;

public interface IParticipationClientProcedures
{
    Task Receive(IEnumerable<Snapshot> snapshots);
    Task<IEnumerable<ParticipationModel>> GetActive();
}

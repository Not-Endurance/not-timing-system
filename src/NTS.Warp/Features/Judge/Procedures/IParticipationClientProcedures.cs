using NTS.Domain.Objects;
using NTS.Warp.Features.Judge.Models;

namespace NTS.Warp.Features.Judge.Procedures;

public interface IParticipationClientProcedures
{
    Task ProcessSnapshots(IEnumerable<Snapshot> snapshots);
    Task<IEnumerable<ParticipationWarpDto>> GetActiveParticipations();
}

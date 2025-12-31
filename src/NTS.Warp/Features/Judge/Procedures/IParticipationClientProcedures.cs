using NTS.Application.Models;
using NTS.Domain.Objects;

namespace NTS.Warp.Features.Judge.Procedures;

public interface IParticipationClientProcedures
{
    Task ProcessSnapshots(IEnumerable<Snapshot> snapshots);
    Task<IEnumerable<CoreParticipationModel>> GetActiveParticipations();
}

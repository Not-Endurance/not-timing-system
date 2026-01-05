using NTS.Domain.Core.Aggregates;

namespace NTS.Witness.Services;

public interface ISnapshotService
{
    List<Participation> GetParticipations();
}

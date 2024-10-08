using NTS.Domain.Core.Aggregates.Participations;
using NTS.Persistence.States;

namespace NTS.Persistence.Adapters;

public class SnapshotResultRepository(IStore<CoreState> store) : SetRepository<SnapshotResult, CoreState>(store)
{
}

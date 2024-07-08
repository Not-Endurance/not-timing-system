﻿using NTS.Domain.Core.Aggregates.Participations;
using NTS.Persistence.States;

namespace NTS.Persistence.Adapters;

public class SnapshotResultRepository : SetRepository<SnapshotResult, CoreState>
{
    public SnapshotResultRepository(IStore<CoreState> store) : base(store)
    {
    }
}
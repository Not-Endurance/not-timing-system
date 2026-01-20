using NTS.Domain.Enums;
using NTS.Domain.Watcher;

namespace NTS.Application.Models;

public class SnapshotModel
{
    public SnapshotModel(IEnumerable<IntermediateSnapshot> snapshots, string type)
    {
        Entries = snapshots;
        if (type == Arrival_string)
        {
            Type = SnapshotType.Stage;
        }
        else
        {
            Type = SnapshotType.Vet;
        }
    }
    public IEnumerable<IntermediateSnapshot> Entries { get; set; } = [];

    public SnapshotType Type { get; set; }
}


using Not.Domain.Base;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Domain.Watcher;

public record SnapshotParticipant : DomainObject
{
    public SnapshotParticipant(int number, Person athlete, Timestamp timestamp)
    {
        Number = number;
        Athlete = athlete;
        Timestamp = timestamp;
    }

    public SnapshotParticipant(int number, Person athlete)
    {
        Number = number;
        Athlete = athlete;
    }

    public int Number { get; }
    public Person Athlete { get; }
    public Timestamp? Timestamp { get; set; }

    public override string ToString()
    {
        return $"#{Number}: {Athlete}";
    }
}



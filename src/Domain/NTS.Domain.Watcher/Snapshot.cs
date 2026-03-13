using NTS.Domain.Objects;

namespace NTS.Domain.Watcher;

public record Snapshot
{
    public Snapshot(int number, Person athlete, Timestamp timestamp)
    {
        Number = number;
        Athlete = athlete;
        Timestamp = timestamp;
    }

    public Snapshot(int number, Person athlete)
    {
        Number = number;
        Athlete = athlete;
    }

    public int Number { get; }
    public Person Athlete { get; }
    public Timestamp Timestamp { get; set; } = default!;

    public override string ToString()
    {
        return $"#{Number}: {Athlete}";
    }
}

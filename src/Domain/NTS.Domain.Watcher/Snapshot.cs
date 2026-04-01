using Not.Structures;
using NTS.Domain.Objects;

namespace NTS.Domain.Watcher;

public record Snapshot : IIdentifiable
{
    public Snapshot(int number, Person athlete, Timestamp? timestamp = null)
    {
        Number = number;
        Athlete = athlete;
        Timestamp = timestamp;
    }

    public int Number { get; }
    public int Id => Number;
    public Person Athlete { get; }
    public Timestamp? Timestamp { get; set; }

    public override string ToString()
    {
        return $"#{Number}: {Athlete}";
    }
}

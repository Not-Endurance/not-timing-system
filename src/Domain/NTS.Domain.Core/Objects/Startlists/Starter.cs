using Not.Formatting;

namespace NTS.Domain.Core.Objects.Startlists;

public record Starter : ValueObject
{
    internal Starter(Person athlete, int number, int phaseNumber, double distance, Timestamp start)
    {
        Athlete = athlete;
        Number = number;
        PhaseNumber = phaseNumber;
        Distance = distance;
        Start = start;
    }

    public Person Athlete { get; }
    public int Number { get; }
    public int PhaseNumber { get; }
    public double Distance { get; }
    public StartlistEntryState State { get; internal set; } = StartlistEntryState.Resting;
    public Timestamp Start { get; }

    public override string ToString()
    {
        var distance = Distance + km_string;
        var startTime = Start.ToTimeSpan();
        var start = FormattingHelper.Format(startTime);
        return Combine(Number, Athlete, distance, start);
    }
}

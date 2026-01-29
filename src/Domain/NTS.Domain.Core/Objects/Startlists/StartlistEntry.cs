using Not.Domain.Exceptions;
using Not.Formatting;
using NTS.Domain.Core.Aggregates;

namespace NTS.Domain.Core.Objects.Startlists;

public record StartlistEntry
{
    public StartlistEntry(Person athlete, int number, int loopNumber, double distance, Timestamp startAt)
    {
        Athlete = athlete;
        Number = number;
        PhaseNumber = loopNumber;
        Distance = distance;
        Start = startAt;
    }

    public StartlistEntry(Participation participation)
    {
        Athlete = participation.Combination.Athlete.Names;
        Number = participation.Combination.Number;
        var nextPhase = participation.Phases.GetNext();
        PhaseNumber = participation.Phases.NumberOf(nextPhase);
        Distance = nextPhase.Length;
        Start = new Timestamp((nextPhase.StartTime ?? Timestamp.DEFAULT).ToDateTimeOffset());
    }

    public Person Athlete { get; }
    public int Number { get; }
    public int PhaseNumber { get; }
    public double Distance { get; }
    public StartlistEntryState State { get; internal set; } = StartlistEntryState.Resting;

    // TODO: Use Timestamp instead and use TimeOfDay internally in Timestamp in order to discard Day differences. Should make testing a bit more easier with actual data
    public Timestamp Start { get; }

    public override string ToString()
    {
        var distance = Distance + km_string;
        var start = FormattingHelper.Format(Start.ToTimeSpan());
        var result = DomainModelHelper.Combine(Number, Athlete, distance, start);
        return result;
    }
}

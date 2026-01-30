using Not.Formatting;

namespace NTS.Domain.Core.Objects.Startlists;

public record StartlistEntry : ValueObject
{
    public StartlistEntry(Person athlete, int number, int phaseNumber, double distance, Timestamp start)
    {
        Athlete = athlete;
        Number = number;
        PhaseNumber = phaseNumber;
        Distance = distance;
        //<<<<<<< HEAD
        //        Start = startAt;
        //    }

        //    public StartlistEntry(Participation participation)
        //    {
        //        Athlete = participation.Combination.Athlete.Names;
        //        Number = participation.Combination.Number;
        //        var nextPhase = participation.Phases.GetNext();
        //        PhaseNumber = participation.Phases.NumberOf(nextPhase);
        //        Distance = nextPhase.Length;
        //        Start = new Timestamp((nextPhase.StartTime ?? Timestamp.DEFAULT).ToDateTimeOffset());
        //=======
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
        var start = FormattingHelper.Format(Start.ToTimeSpan());
        return Combine(Number, Athlete, distance, start);
    }
}

using Not.Domain.Base;
using Not.Localization;
using NTS.Domain.Core.Aggregates;

namespace NTS.Domain.Core.Objects.Startlists;

public record StartlistEntry : DomainObject
{
    public StartlistEntry(
        Person athlete,
        int number,
        int loopNumber,
        double distance,
        DateTimeOffset startAt
    )
    {
        Athlete = athlete;
        Number = number;
        PhaseNumber = loopNumber;
        Distance = distance;
        Time = startAt;
    }

    public StartlistEntry(Participation participation)
    {
        Athlete = participation.Combination.Athlete.Names;
        Number = participation.Combination.Number;
        var currentIndex = participation.Phases.IndexOf(participation.Phases.Current);
        var nextPhase = participation.Phases[currentIndex + 1];
        PhaseNumber = participation.Phases.NumberOf(nextPhase);
        Distance = nextPhase.Length;
        Time = nextPhase.StartTime!.ToDateTimeOffset();
    }

    public Person Athlete { get; }
    public int Number { get; }
    public int PhaseNumber { get; }
    public double Distance { get; }

    // TODO: Use Timestamp instead and use TimeOfDay internally in Timestamp in order to discard Day differences. Should make testing a bit more easier with actual data
    public DateTimeOffset Time { get; }

    public override string ToString()
    {
        var distance = Distance + "km".Localize();
        var result = Combine(Number, Athlete, distance, Time.TimeOfDay);
        return result;
    }
}

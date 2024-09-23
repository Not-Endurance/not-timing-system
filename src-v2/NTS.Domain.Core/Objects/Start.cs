using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain;

namespace NTS.Domain.Core.Objects;

public class StartList
{
    public const string START_HISTORY = "start_history";
    public const string UPCOMING_STARTS = "upcoming_starts";
    public StartList(IEnumerable<Participation> participations,string type)
    {
        if(type==UPCOMING_STARTS)
        {
            foreach (var participation in participations)
            {
                if (!participation.IsNotQualified)
                {
                    var phase = participation.Phases.Current;
                    if (phase.OutTime != null && phase.StartTime != null)
                    {
                        var upcomingStart = new Start(participation);
                        Starts.Add(upcomingStart);
                    }
                }
            }
        }
        else
        {
            foreach (var participation in participations)
            {
                foreach(var phase in participation.Phases)
                {
                    var loopNumber = participation.Phases.NumberOf(phase ?? participation.Phases.Last());
                    if(phase.OutTime!=null)
                    {
                        var startRecord = new Start(participation.Tandem.Name, participation.Tandem.Number, loopNumber, phase.Length, participation.Phases.Distance, phase, phase.OutTime != null ? phase.OutTime : phase.StartTime);
                        Starts.Add(startRecord);
                    }
                }
            }
        }
    }
    
    public List<Start> Starts = new List<Start>();

    public IEnumerable<Start> StartlistByDistance()
    {
        return Starts
            .GroupBy(sh => Tuple.Create(sh.TotalDistance, sh.Athlete))
            .OrderBy(g => g.Key.Item1)
            .ThenBy(g => g.Key.Item2) //returns IOrderedEnumerable<IGrouping<Tuple<double, Person>, Start>>
            .FlattenGroupedItems();
    }
}
public class Start : IComparable<Start>
{
    public Start(Participation participation)
    {
        Athlete = participation.Tandem.Name;
        Number = participation.Tandem.Number;
        LoopNumber = participation.Phases.CurrentNumber;
        Distance = participation.Phases.Current.Length;
        StartAt = participation.Phases.OutTime != null? participation.Phases.OutTime : participation.Phases.Current.StartTime;
        GuardHelper.ThrowIfDefault(participation?.Phases?.Current);
        CurrentPhase = participation?.Phases?.Current;
        TotalDistance = participation!.Phases!.Distance;
    }

    public Start(Person athlete, int number, int loopNumber, double distance, double totalDistance, Phase phase, Timestamp startAt)
    {
        Athlete = athlete;
        Number = number;
        LoopNumber = loopNumber;
        Distance = distance;
        TotalDistance = totalDistance;
        CurrentPhase = phase;
        StartAt = startAt;
    }

    public Person Athlete { get; private set; }
    public int Number { get; private set; }
    public int LoopNumber { get; private set; }
    public double Distance { get; private set; }
    public double TotalDistance { get; private set; }
    public Phase CurrentPhase { get; private set; }
    public Timestamp? StartAt { get; private set; }

    public string StartIn()
    {
        if(StartAt == null) return string.Empty;
        if (StartAt > Timestamp.Now())
        {
            return (StartAt - Timestamp.Now()).ToString();
        }
        else
        {
            return "-" + (StartAt - Timestamp.Now()).ToString();
        }
        //test cases -remove before merge
        //return "00:05:02";
        //return "00:00:03";
        //return "-00:14:57";
    }

    public override string ToString()
    {
        var result = $"{Number}, {Athlete}, #{LoopNumber}: {Distance}km, {StartAt}";
        return result;
    }

    public int CompareTo(Start other)
    {
        if (other == null) return 1;

        // First compare Athlete
        int athleteComparison = string.Compare(Athlete, other.Athlete, StringComparison.Ordinal);
        if (athleteComparison != 0) return athleteComparison;

        // Then compare Competition Distance if Athlete is the same
        return string.Compare(TotalDistance.ToString(), other.TotalDistance.ToString(), StringComparison.Ordinal);
    }
}
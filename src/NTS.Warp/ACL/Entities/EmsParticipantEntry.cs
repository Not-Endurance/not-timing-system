using NTS.Warp.ACL.Entities.Participations;

namespace NTS.Warp.ACL.Entities;

public class EmsParticipantEntry : IComparable<EmsParticipantEntry>, IEquatable<EmsParticipantEntry>
{
    public EmsParticipantEntry() { }

    public EmsParticipantEntry(EmsParticipation participation)
    {
        Number = participation.Participant.Number;
        Name = participation.Participant.Athlete.Name;
        LapNumber = participation.Participant.LapRecords.Count;
        LapDistance = participation.Distance ?? 0;
        ArriveTime = participation.Participant.LapRecords.LastOrDefault(x => x.ArrivalTime != null)?.ArrivalTime;
    }

    public int LapNumber { get; init; }
    public string Number { get; init; }
    public string Name { get; init; }
    public DateTimeOffset? ArriveTime { get; set; }
    public double LapDistance { get; set; }

    public int CompareTo(EmsParticipantEntry other)
    {
        var thisNumber = int.Parse(Number);
        var otherNumber = int.Parse(other.Number);
        if (thisNumber < otherNumber)
        {
            return -1;
        }
        return 1;
    }

    public override bool Equals(object obj)
    {
        if (obj is EmsParticipantEntry entry)
        {
            return Equals(entry);
        }
        return false;
    }

    public bool Equals(EmsParticipantEntry other)
    {
        return Number == other?.Number;
    }

    public override int GetHashCode()
    {
        return Number.GetHashCode();
    }
}

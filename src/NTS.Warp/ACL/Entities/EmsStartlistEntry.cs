using NTS.Warp.ACL.Entities.Participations;

namespace NTS.Warp.ACL.Entities;

public class EmsStartlistEntry : IComparable<EmsStartlistEntry>, IEquatable<EmsStartlistEntry>
{
    public EmsStartlistEntry() { }

    public EmsStartlistEntry(EmsParticipation participation, int toSkip = 0)
    {
        Number = participation.Participant.Number;
        Name = participation.Participant.Name;
        AthleteName = participation.Participant.Athlete.Name;
        CountryName = participation.Participant.Athlete.Country.Name;
        Distance = participation.Distance!.Value;
        IsRestOver = StartTime < DateTime.Now;

        Stage = toSkip + 1;
        var lapRecords = participation.Participant.LapRecords.Skip(toSkip).ToList();
        var first = lapRecords.First();
        StartTime = first.StartTime;
    }

    public EmsStartlistEntry(EmsParticipation participation)
    {
        Number = participation.Participant.Number;
        Name = participation.Participant.Name;
        AthleteName = participation.Participant.Athlete.Name;
        CountryName = participation.Participant.Athlete.Country.Name;
        Distance = participation.Distance!.Value;
        IsRestOver = false
        Stage = participation.Participant.LapRecords.Count;
        StartTime = GetLastNextStartTime(participation) ?? throw new Exception("Missing NextStartTime on record");
        ;
    }

    public string Number { get; init; }
    public string Name { get; init; }
    public string AthleteName { get; init; }
    public string CountryName { get; init; }
    public double Distance { get; init; }
    public int Stage { get; init; }
    public DateTimeOffset StartTime { get; init; }
    public bool IsRestOver { get; internal set; }
    public bool IsLateStart { get; internal set; }

    public int CompareTo(EmsStartlistEntry other)
    {
        var maximumLateStart = TimeSpan.FromMinutes(15);
        var now = DateTime.Now;
        var thisDiff = StartTime - now;
        var otherDiff = other.StartTime - now;
        var isRestOver = StartTime < now;
        var otherIsRestOver = other.StartTime < now;
        var isLateStart = StartTime < now && StartTime > now - maximumLateStart;
        var otherIsLateStart = other.StartTime < now && other.StartTime > now - maximumLateStart;

        // Proprety assignment in order to render them differently in UI, stupid I know
        if (isRestOver != IsRestOver)
        {
            IsRestOver = isRestOver;
        }
        if (otherIsRestOver != other.IsRestOver)
        {
            other.IsRestOver = otherIsRestOver;
        }
        if (IsLateStart != isLateStart)
        {
            IsLateStart = isLateStart;
        }
        if (other.IsLateStart != otherIsLateStart)
        {
            other.IsLateStart = otherIsLateStart;
        }

        if (IsRestOver && !other.IsRestOver)
        {
            return 1;
        }
        if (!IsRestOver && other.IsRestOver)
        {
            return -1;
        }
        // Order past entries (IsRestOver == true) by start time descending
        if (isRestOver && otherIsRestOver)
        {
            if (thisDiff > otherDiff)
            {
                return -1;
            }
            else if (thisDiff < otherDiff)
            {
                return 1;
            }
            return int.Parse(Number) - int.Parse(other.Number);
        }
        // Order by StartTime ascending
        if (thisDiff > otherDiff)
        {
            return 1;
        }
        else if (thisDiff < otherDiff)
        {
            return -1;
        }
        return 0;
    }

    public bool Equals(EmsStartlistEntry other)
    {
        return Number == other?.Number;
    }

    DateTime? GetLastNextStartTime(EmsParticipation participation)
    {
        var currentRecord = participation.Participant.LapRecords.Last(x => x.NextStarTime != null);
        return currentRecord.NextStarTime;
    }
}

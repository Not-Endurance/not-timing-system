using System;
using System.Linq;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Witness.RPC;

public class WitnessStartlist : List<StartlistEntry>
{
    static readonly TimeSpan RECENT_WINDOW = TimeSpan.FromMinutes(15);

    public WitnessStartlist() { }

    public WitnessStartlist(IEnumerable<StartlistEntry> entries)
    {
        AddRange(entries);
        SortInternal();
    }

    public IReadOnlyList<StartlistEntry> Upcoming
    {
        get
        {
            var now = DateTimeOffset.Now;
            return this.Where(entry => now - entry.Time <= RECENT_WINDOW)
                .OrderBy(entry => entry.Time)
                .ThenBy(entry => entry.PhaseNumber)
                .ToList();
        }
    }

    public IReadOnlyList<StartlistEntry> History
    {
        get
        {
            var now = DateTimeOffset.Now;
            return this.Where(entry => now - entry.Time > RECENT_WINDOW)
                .OrderByDescending(entry => entry.Time)
                .ThenBy(entry => entry.PhaseNumber)
                .ToList();
        }
    }

    public void Update(StartlistEntry entry, WitnessCollectionAction action)
    {
        var existingIndex = FindIndex(x => IsSameEntry(x, entry));
        if (existingIndex >= 0)
        {
            RemoveAt(existingIndex);
        }

        if (action == WitnessCollectionAction.AddOrUpdate)
        {
            Add(entry);
        }

        SortInternal();
    }

    static bool IsSameEntry(StartlistEntry left, StartlistEntry right)
    {
        return left.Number == right.Number && left.PhaseNumber == right.PhaseNumber;
    }

    void SortInternal()
    {
        Sort(
            (a, b) =>
            {
                var timeComparison = DateTimeOffset.Compare(a.Time, b.Time);
                if (timeComparison != 0)
                {
                    return timeComparison;
                }
                return a.PhaseNumber.CompareTo(b.PhaseNumber);
            }
        );
    }
}

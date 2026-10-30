using NTS.Domain.Core.Aggregates;
using static Not.Localization.NStrings;

namespace NTS.Contracts.Features.Console.Rankings;

/// <summary>A Participation that a custom Ranking counts, and whether the Ranking leaves it out of the placings.</summary>
public class CustomRankingEntryModel
{
    public Participation? Participation { get; set; }
    public bool IsNotRanked { get; set; }

    public override string ToString()
    {
        return Participation == null ? string.Empty
            : IsNotRanked ? $"{X_string} {Participation}"
            : Participation.ToString();
    }
}

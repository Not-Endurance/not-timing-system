namespace NTS.Domain.Core.Aggregates;

/// <summary>How a ride placed in a Ranking, as a list of the rides of an Athlete or a Horse shows it (#638).</summary>
public enum PlacingKind
{
    /// <summary>A place: the rank of its entry.</summary>
    Ranked = 1,

    /// <summary>The entry is marked not ranked: the ride is listed, and does not compete for a place.</summary>
    NotRanked = 2,

    /// <summary>The ride was eliminated: the code the Results show stands for the place.</summary>
    Eliminated = 3,
}

/// <summary>
/// The placing of one ride in one Ranking (#638, ADR-0006): a number, the code of the elimination as the Results show it, or not
/// ranked. A Ranking gives it with <see cref="Ranking.PlacingOf"/>.
/// </summary>
public sealed record Placing
{
    public static Placing Ranked(int rank)
    {
        return new Placing(PlacingKind.Ranked, rank, null);
    }

    public static Placing NotRanked()
    {
        return new Placing(PlacingKind.NotRanked, null, null);
    }

    public static Placing Eliminated(string code)
    {
        return new Placing(PlacingKind.Eliminated, null, code);
    }

    Placing(PlacingKind kind, int? rank, string? code)
    {
        Kind = kind;
        Rank = rank;
        Code = code;
    }

    public PlacingKind Kind { get; }

    /// <summary>The place of a ride that is ranked, none otherwise.</summary>
    public int? Rank { get; }

    /// <summary>The elimination as the Results show it (<c>FTQ GA</c>, <c>RET</c>, <c>DQ ...</c>), none for a ride that was not eliminated.</summary>
    public string? Code { get; }
}

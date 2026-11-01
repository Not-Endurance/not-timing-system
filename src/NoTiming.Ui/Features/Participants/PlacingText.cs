using NTS.Domain.Core.Aggregates;

namespace NoTiming.Ui.Features.Participants;

/// <summary>What a placing is called (#639): the Results' word for it, a place as its number.</summary>
public static class PlacingText
{
    public static string Of(Placing? placing)
    {
        return placing?.Kind switch
        {
            PlacingKind.Ranked => placing.Rank.ToString()!,
            PlacingKind.Eliminated => $"{Eliminated_string}: {placing.Code}",
            PlacingKind.NotRanked => Not_ranked_string,
            _ => string.Empty,
        };
    }
}

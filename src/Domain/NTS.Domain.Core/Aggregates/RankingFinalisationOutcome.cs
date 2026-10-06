namespace NTS.Domain.Core.Aggregates;

/// <summary>What finalising a Ranking came to (ADR-0006, #640).</summary>
public enum RankingFinalisationOutcome
{
    /// <summary>The ranks were composed and are in the Ranking that comes back.</summary>
    Finalised = 1,

    /// <summary>Every entry already held its rank: nothing to do.</summary>
    AlreadyFinal = 2,

    /// <summary>Some entries hold a rank and some do not, a state nothing produces: it is reported and left alone.</summary>
    SomePlacingsStored = 3,
}

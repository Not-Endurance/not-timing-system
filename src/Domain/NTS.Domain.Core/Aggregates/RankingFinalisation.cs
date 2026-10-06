namespace NTS.Domain.Core.Aggregates;

/// <summary>
/// A Ranking after it was asked to finalise its placings (ADR-0006, #640): the Ranking to keep, which is the one with the
/// stored ranks when they were stored and the Ranking itself otherwise, and what came of it.
/// </summary>
public sealed class RankingFinalisation
{
    public RankingFinalisation(Ranking ranking, RankingFinalisationOutcome outcome)
    {
        Ranking = ranking;
        Outcome = outcome;
    }

    public Ranking Ranking { get; }
    public RankingFinalisationOutcome Outcome { get; }
}

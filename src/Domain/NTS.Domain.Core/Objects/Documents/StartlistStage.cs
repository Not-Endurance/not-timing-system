using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Domain.Core.Objects.Documents;

/// <summary>The starts of one stage of the Event, which is a Phase of its Participations, by the time they start.</summary>
public sealed record StartlistStage
{
    public StartlistStage(int number, IReadOnlyList<Starter> starters)
    {
        Number = number;
        Starters = starters;
    }

    public int Number { get; }
    public IReadOnlyList<Starter> Starters { get; }
}

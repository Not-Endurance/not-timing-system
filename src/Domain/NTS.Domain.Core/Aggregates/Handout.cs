using Newtonsoft.Json;
using Not.Domain.Aggregates;

namespace NTS.Domain.Core.Aggregates;

public class Handout : AggregateRoot, IAggregateRoot
{
    [JsonConstructor]
    Handout(int id, Participation participation)
        : base(id)
    {
        Participation = participation;
    }

    public Handout(Participation participation)
        : this(GenerateId(), participation) { }

    public Participation Participation { get; }
}

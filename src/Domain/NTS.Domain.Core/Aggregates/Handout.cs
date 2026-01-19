using Newtonsoft.Json;

namespace NTS.Domain.Core.Aggregates;

public class Handout : Aggregate
{
    public Handout(int? id, Participation participation)
        : base(id)
    {
        Participation = participation;
    }

    public Participation Participation { get; }
}

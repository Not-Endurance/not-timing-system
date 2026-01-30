using Newtonsoft.Json;

namespace NTS.Domain.Core.Aggregates;

public class Handout : Aggregate
{
    public Handout(Participation participation, int? id = null)
        : base(id)
    {
        Participation = participation;
    }

    public Participation Participation { get; }
}

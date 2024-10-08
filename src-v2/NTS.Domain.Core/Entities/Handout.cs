using Newtonsoft.Json;
using NTS.Domain.Core.Aggregates.Participations;

namespace NTS.Domain.Core.Entities;

public class Handout : DomainEntity
{
    // TODO: Investigate why this cannot deserialize without private ctor, but Officials can
    // It's because Official's parameters are identical to it's properties while Hangout's arent
    // How to approach this consistently?
    [JsonConstructor]
    private Handout(int id) : base(id)
    {
    }
    public Handout(Participation participation)
    {
        Participation = participation;
    }

    public Participation Participation { get; private set; }
}

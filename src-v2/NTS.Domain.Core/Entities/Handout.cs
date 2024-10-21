using Newtonsoft.Json;

namespace NTS.Domain.Core.Entities;

public class Handout : DomainEntity
{
    [JsonConstructor]
    private Handout(int id, Participation participation) : base(id)
    {
        Participation = participation;
    }
    public Handout(Participation participation) : this(GenerateId(), participation)
    {
    }

    public Participation Participation { get; private set; }
}

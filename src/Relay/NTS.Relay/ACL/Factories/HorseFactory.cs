using NTS.Relay.ACL.Entities.Horses;
using NTS.Relay.ACL.Models;
using NTS.Domain.Core.Aggregates;

namespace NTS.Relay.ACL.Factories;

public class HorseFactory
{
    public static EmsHorse Create(Participation participation)
    {
        var state = new EmsHorseState { Name = participation.Combination.Horse.Name };
        return new EmsHorse(state);
    }
}

using NTS.Domain.Core.Aggregates;
using NTS.Warp.ACL.Entities.Horses;
using NTS.Warp.ACL.Models;

namespace NTS.Warp.ACL.Factories;

public class HorseFactory
{
    public static EmsHorse Create(Participation participation)
    {
        var state = new EmsHorseState { Name = participation.Combination.Horse.Name };
        return new EmsHorse(state);
    }
}

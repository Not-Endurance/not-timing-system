using NTS.Application.Models;
using NTS.Warp.ACL.Entities.Horses;
using NTS.Warp.ACL.Models;

namespace NTS.Warp.ACL.Factories;

public class HorseFactory
{
    public static EmsHorse Create(CoreHorseModel horse)
    {
        var state = new EmsHorseState { Name = horse.Name };
        return new EmsHorse(state);
    }
}

using NTS.Warp.ACL.Entities.Horses;
using NTS.Warp.ACL.Models;
using NTS.Warp.Features.Judge.Models;

namespace NTS.Warp.ACL.Factories;

public class HorseFactory
{
    public static EmsHorse Create(ParticipationWarpDto.HorseDto horse)
    {
        var state = new EmsHorseState { Name = horse.Name };
        return new EmsHorse(state);
    }
}

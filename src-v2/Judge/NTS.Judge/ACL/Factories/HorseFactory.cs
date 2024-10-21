using NTS.Compatibility.EMS.Entities.Horses;
using NTS.Domain.Core.Entities;
using NTS.Judge.ACL.Bridge;

namespace NTS.Judge.ACL.Factories;

public class HorseFactory
{
    public static EmsHorse Create(Participation participation)
    {
        var state = new EmsHorseState
        {
            Name = participation.Tandem.Horse
        };
        return new EmsHorse(state);
    }
}

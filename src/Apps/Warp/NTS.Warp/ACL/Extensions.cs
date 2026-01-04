using NTS.Domain.Enums;
using NTS.Warp.ACL.Enums;

namespace NTS.Warp.ACL;

public static class Extensions
{
    public static ParticipationCategory ToNtsCategory(this EmsCategory category)
    {
        return category switch
        {
            EmsCategory.Seniors => ParticipationCategory.Senior,
            EmsCategory.Children => ParticipationCategory.Children,
            EmsCategory.JuniorOrYoungAdults => ParticipationCategory.JuniorOrYoungAdult,
            _ => throw new NotImplementedException(),
        };
    }
}

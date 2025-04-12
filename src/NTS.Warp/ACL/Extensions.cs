using NTS.Domain.Enums;
using NTS.Warp.ACL.Enums;

namespace NTS.Warp.ACL;

public static class Extensions
{
    public static AthleteCategory ToNtsCategory(this EmsCategory category)
    {
        return category switch
        {
            EmsCategory.Seniors => AthleteCategory.Senior,
            EmsCategory.Children => AthleteCategory.Children,
            EmsCategory.JuniorOrYoungAdults => AthleteCategory.JuniorOrYoungAdult,
            _ => throw new NotImplementedException(),
        };
    }
}

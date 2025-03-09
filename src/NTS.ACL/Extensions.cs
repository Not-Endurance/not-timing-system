using NTS.ACL.Enums;
using NTS.Domain.Enums;

namespace NTS.ACL;

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

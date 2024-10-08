using NTS.Domain.Setup.Entities;

namespace NTS.Judge.Events;

public class CompetitionChildrenBehind //: INotSetBehind<Contestant>, INotSetBehind<Phase>, INotParentBehind<Competition>
{
    public static Competition? Competition { get; internal set; }


}

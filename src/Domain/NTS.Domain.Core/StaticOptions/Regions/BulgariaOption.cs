using NTS.Domain.Core.StaticOptions.Regions.Base;

namespace NTS.Domain.Core.StaticOptions.Regions;

public class BulgariaOption : RegionOption
{
    public BulgariaOption()
        : base("BGR") // TODO: figure out country infra
    {
        ShouldOnlyUseAverageLoopSpeed = true;
    }
}

using NoTiming.Domain.Aggregates;
using NoTiming.Domain.Core.StaticOptions.Regions;
using NoTiming.Domain.Core.StaticOptions.Regions.Base;

namespace NoTiming.Domain.Core.StaticOptions;

internal class RegionOptionProvider
{
    public static IRegionOption? Get(Country? country)
    {
        return _configurations.FirstOrDefault(x => x.CountryIsoCode == country?.IsoCode);
    }

    static RegionOption[] _configurations = [new BulgariaOption()];
}

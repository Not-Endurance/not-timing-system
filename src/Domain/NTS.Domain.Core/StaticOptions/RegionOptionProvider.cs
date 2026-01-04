using NTS.Domain.Aggregates;
using NTS.Domain.Core.StaticOptions.Regions;
using NTS.Domain.Core.StaticOptions.Regions.Base;

namespace NTS.Domain.Core.StaticOptions;

internal class RegionOptionProvider
{
    public static IRegionOption? Get(Country? country)
    {
        return _configurations.FirstOrDefault(x => x.CountryIsoCode == country?.IsoCode);
    }

    static RegionOption[] _configurations = [new BulgariaOption()];
}

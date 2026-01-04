namespace NTS.Domain.Core.StaticOptions.Regions.Base;

public abstract class RegionOption : IRegionOption
{
    protected RegionOption(string countryIsoCode)
    {
        CountryIsoCode = countryIsoCode;
    }

    public string CountryIsoCode { get; set; }
    public bool ShouldOnlyUseAverageLoopSpeed { get; set; }
}

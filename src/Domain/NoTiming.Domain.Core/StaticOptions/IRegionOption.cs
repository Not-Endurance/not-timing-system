namespace NoTiming.Domain.Core.StaticOptions;

public interface IRegionOption
{
    string CountryIsoCode { get; }
    bool ShouldOnlyUseAverageLoopSpeed { get; }
}

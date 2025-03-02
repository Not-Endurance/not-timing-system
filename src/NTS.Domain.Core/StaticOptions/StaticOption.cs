using Not.Domain.Ports;
using Not.Startup;
using NTS.Domain.Aggregates;

namespace NTS.Domain.Core.StaticOptions;

// TODO: StaticOptions needs a redesign as it is too intertwined with infrastructure to be in the Domain layer
public class StaticOption : IStartupInitializer
{
    public static bool IsRfidDetectionEnabled()
    {
        return Detection != default && Detection == DetectionMode.Rfid;
    }

    public static SnapshotType GetRfidSnapshotType()
    {
        if (_options == null)
        {
            throw new GuardException(
                "Internal options property was not found. Check if static-options.json is configured."
            );
        }
        return _options.RfidSnapshotType;
    }

    public static bool IsVisionDetectionEnabled()
    {
        return Detection != default && Detection == DetectionMode.ComputerVision;
    }

    public static bool ShouldOnlyUseAverageLoopSpeed(CompetitionRuleset ruleset)
    {
        if (ruleset == CompetitionRuleset.Regional && Regional != null)
        {
            return Regional.ShouldOnlyUseAverageLoopSpeed;
        }
        return false;
    }

    public static bool ShouldUseRegionalRanker(CompetitionRuleset ruleset)
    {
        if (ruleset == CompetitionRuleset.Regional)
        {
            return true;
        }
        return false;
    }

    #region Instance is used in order to be initialized on Startup

    readonly IStaticOptionsProvider<Model> _provider;
    static Model? _options;

    public StaticOption(IStaticOptionsProvider<Model> provider)
    {
        _provider = provider;
    }

    public static IRegionOption? Regional { get; private set; }
    public static Country[] Countries { get; private set; } = [];
    public static Country? SelectedCountry { get; private set; }
    public static DetectionMode? Detection { get; private set; }

    public void RunAtStartup()
    {
        _options = _provider.Get();
        SelectedCountry = _options.SelectedCountry;
        Countries = _options.Countries;
        Regional = RegionOptionProvider.Get(_options.SelectedCountry);
        Detection = _options.DetectionMode;
    }

    #endregion

    public class Model
    {
        public Country[] Countries { get; set; } = [];
        public Country? SelectedCountry { get; set; }
        public DetectionMode DetectionMode { get; set; }
        public SnapshotType RfidSnapshotType { get; set; }
    }
}

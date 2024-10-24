using Not.Domain.Ports;
using Not.Injection;
using Not.Startup;

namespace NTS.Domain.Core.Configuration;

public class StaticOptions : IStartupInitializer, ISingletonService
{
    public static IRegionalConfiguration? RegionalConfiguration { get; private set; }
    public static Country[] Countries { get; private set; } = [];
    public static Country? SelectedCountry { get; private set; }
    public static DetectionMode? Detection { get; private set; }
    
    static Model? _options;

    public static bool IsRfidDetectionEnabled()
    {
        return Detection != default && Detection == DetectionMode.Rfid;
    }

    public static SnapshotType GetRfidSnapshotType()
    {
        if (_options == null)
        {
            throw new GuardException("Internal options property was not found. Check if static-options.json is configured.");
        }
        return _options.RfidSnapshotType;
    }

    public static bool IsVisionDetectionEnabled()
    {
        return Detection != default && Detection == DetectionMode.ComputerVision;
    }

    public static bool ShouldOnlyUseAverageLoopSpeed(CompetitionRuleset ruleset)
    {
        if (!ShouldUseRegionalConfiguration(ruleset))
        {
            return RegionalConfiguration!.ShouldOnlyUseAverageLoopSpeed;
        }
        return false;
    }

    public static bool ShouldUseRegionalRanker(CompetitionRuleset ruleset)
    {
        if (!ShouldUseRegionalConfiguration(ruleset))
        {
            return false;
        }
        return true;
    }

    static bool ShouldUseRegionalConfiguration(CompetitionRuleset ruleset)
    {
        return ruleset == CompetitionRuleset.Regional && RegionalConfiguration != null;
    }

    #region Instance is used in order to be initialized on Startup

    readonly IStaticOptionsProvider<Model> _provider;

    public StaticOptions(IStaticOptionsProvider<Model> provider)
    {
        _provider = provider;
    }

    public void RunAtStartup()
    {
        var options = _provider.Get();
        SelectedCountry = options.SelectedCountry;
        RegionalConfiguration = RegionalConfigurationProvider.Get(options.SelectedCountry);
        Detection = options.DetectionMode;
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

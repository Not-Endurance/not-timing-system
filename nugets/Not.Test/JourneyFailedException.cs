namespace Not.Test;

/// <summary>
/// A journey that stopped at a stage. The message names the feature, the journey, the scenario and the stage, and the
/// failure of the stage is the inner exception.
/// </summary>
public sealed class JourneyFailedException : Exception
{
    public JourneyFailedException(string feature, string journey, string scenario, JourneyStage stage, Exception inner)
        : base($"{feature} > {journey} > {scenario} stopped at stage {stage.Number} ({stage.Name}): {inner.Message}", inner)
    {
        Feature = feature;
        Journey = journey;
        Scenario = scenario;
        Stage = stage;
    }

    public string Feature { get; }
    public string Journey { get; }
    public string Scenario { get; }
    public JourneyStage Stage { get; }
}

namespace Not.Test;

/// <summary>
/// Runs the stages of a journey in order, and stops at the first that fails (ADR-0016). The failure names the feature, the
/// journey, the scenario and the stage it stopped at, so that a failing test says where the journey went wrong.
/// </summary>
public static class JourneyRunner
{
    public static async Task RunAsync(string feature, string journey, string scenario, IEnumerable<JourneyStage> stages)
    {
        foreach (var stage in stages)
        {
            try
            {
                await stage.Run();
            }
            catch (Exception exception)
            {
                throw new JourneyFailedException(feature, journey, scenario, stage, exception);
            }
        }
    }
}

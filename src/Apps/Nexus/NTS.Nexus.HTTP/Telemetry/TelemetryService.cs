using System.Diagnostics;

namespace NTS.Nexus.HTTP.Telemetry;

internal class TelemetryService : ITelemetryService
{
    static readonly ActivitySource SOURCE = new(NexusTelemetryConstants.ACTIVITY_SOURCE_NAME);
    static readonly bool DEBUG_ACTIVITY_SOURCE = bool.TryParse(
            Environment.GetEnvironmentVariable("OTEL_DEBUG_ACTIVITY_SOURCE"),
            out var debug
        )
        && debug;

    public Activity? StartActivity(string className, string methodName)
    {
        var activityName = $"{className}.{methodName}";

        if (DEBUG_ACTIVITY_SOURCE)
        {
            Console.WriteLine(
                $"TelemetrySource: creating '{activityName}' (HasListeners={SOURCE.HasListeners()})"
            );
        }

        var activity = SOURCE.StartActivity(activityName, ActivityKind.Internal);
        if (DEBUG_ACTIVITY_SOURCE)
        {
            Console.WriteLine(
                activity == null
                    ? $"TelemetrySource: dropped '{activityName}' (null activity)"
                    : $"TelemetrySource: started '{activity.DisplayName}' (Id={activity.Id})"
            );
        }

        if (activity == null)
        {
            return null;
        }

        activity.SetTag("code.namespace", className);
        activity.SetTag("code.function", methodName);

        return activity;
    }
}

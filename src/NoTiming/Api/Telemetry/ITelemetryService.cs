using System.Diagnostics;

namespace NoTiming.Api.Telemetry;

public interface ITelemetryService
{
    Activity? StartActivity(string className, string methodName);
}

using System.Diagnostics;

namespace NTS.Nexus.HTTP.Telemetry;

public interface ITelemetryService
{
    Activity? StartActivity(string className, string methodName);
}

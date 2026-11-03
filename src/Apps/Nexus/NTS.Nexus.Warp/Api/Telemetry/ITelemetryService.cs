using System.Diagnostics;

namespace NTS.Nexus.Warp.Api.Telemetry;

public interface ITelemetryService
{
    Activity? StartActivity(string className, string methodName);
}

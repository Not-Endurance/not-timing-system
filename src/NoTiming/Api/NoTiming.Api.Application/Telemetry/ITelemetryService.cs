using System.Diagnostics;

namespace NoTiming.Api.Application.Telemetry;

public interface ITelemetryService
{
    Activity? StartActivity(string className, string methodName);
}

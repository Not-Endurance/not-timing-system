using System.Diagnostics;

namespace NTS.Nexus.HTTP.Telemetry;

internal static class ActivityErrorExtensions
{
    public static void AttachToCurrentActivity(this Exception exception)
    {
        var activity = Activity.Current;
        if (activity == null)
        {
            return;
        }

        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.SetTag("error.type", exception.GetType().FullName);
        activity.SetTag("error.message", exception.Message);
        activity.SetTag("error.stacktrace", exception.StackTrace);
        activity.AddEvent(
            new ActivityEvent(
                "exception",
                tags: new ActivityTagsCollection
                {
                    ["exception.type"] = exception.GetType().FullName,
                    ["exception.message"] = exception.Message,
                    ["exception.stacktrace"] = exception.StackTrace,
                }
            )
        );
    }
}

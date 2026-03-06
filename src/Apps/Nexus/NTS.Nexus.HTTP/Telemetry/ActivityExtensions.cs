using System.Diagnostics;

namespace NTS.Nexus.HTTP.Telemetry;

internal static class ActivityExtensions
{
    public static Activity? Tag(this Activity? activity, string key, object? value)
    {
        if (activity == null)
        {
            return null;
        }

        activity.SetTag(key, value);
        return activity;
    }

    public static Activity? TagException(this Activity? activity, Exception exception)
    {
        if (activity == null)
        {
            return null;
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

        return activity;
    }
}

using Not.Exceptions;
using Not.Notifier;
using System.Diagnostics;
using System.Text;

namespace Not.Safe;

// SafeHelper is essentially a centralized error-handler. 
// It's the main character in what I'm calling Safe Pattern:
// - Ensures that all backend entry points are error-handled (especially useful in non-web scenarios)
// - public methods are prefixed with "Safe" and changed to private
// - public methods with OG names are added to invoke the Safe methods within SafeHelper.Run
public static class SafeHelper
{
    public static T? Run<T>(Func<T> action)
    {
        try
        {
            return action();
        }
        catch (DomainExceptionBase validation)
        {
            NotifyHelper.Warn(validation);
            return default;
        }
        catch (Exception ex)
        {
            HandleError(ex);
            return default;
        }
    }

    public static Task RunAsync(Func<Task> action)
    {
        return Task.Run(() => Run(action));
    }

    public static async Task Run(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (DomainExceptionBase validation)
        {
            NotifyHelper.Warn(validation);
        }
        catch (Exception ex)
        {
            HandleError(ex);
        }
    }

    public static async Task<T?> Run<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (DomainExceptionBase validation)
        {
            NotifyHelper.Warn(validation);
            return default;
        }
        catch (Exception ex)
        {
            HandleError(ex);
            return default;
        }
    }

    public static Task RunAsync<T>(Func<T, Task> action, T argument)
    {
        return Task.Run(() => Run(action, argument));
    }

    public static async Task Run<T>(Func<T, Task> action, T argument)
    {
        try
        {
            await action(argument);
        }

        catch (DomainExceptionBase validation)
        {
            NotifyHelper.Warn(validation);
        }
        catch (Exception ex)
        {
            HandleError(ex);
        }
    }

    static void HandleError(Exception ex)
    {
        NotifyHelper.Error(ex);
        WriteToTraceConsole(ex);
    }

    static void WriteToTraceConsole(Exception exception)
    {
        // TODO: add notification
        var sb = new StringBuilder();
        sb.AppendLine("!!!!!!!!!!!!!!!!!!!!!!!!! TASKHELPER EXCEPTION START !!!!!!!!!!!!!!!!!!!!!!!!!");
        sb.AppendLine("!!!!!!!!!!!!!!!!!!!!!!!!! TASKHELPER EXCEPTION START !!!!!!!!!!!!!!!!!!!!!!!!!");
        sb.AppendLine("!!!!!!!!!!!!!!!!!!!!!!!!! TASKHELPER EXCEPTION START !!!!!!!!!!!!!!!!!!!!!!!!!");
        sb.AppendLine("!!!!!!!!!!!!!!!!!!!!!!!!! TASKHELPER EXCEPTION START !!!!!!!!!!!!!!!!!!!!!!!!!");
        sb.AppendLine(exception.Message);
        sb.AppendLine(exception.StackTrace);
        sb.AppendLine("!!!!!!!!!!!!!!!!!!!!!!!!!! TASKHELPER EXCEPTION END !!!!!!!!!!!!!!!!!!!!!!!!!!");

        Trace.WriteLine(sb.ToString(), "console");
    }
}

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
    public static T? Run<T>(Func<T> action, Action<DomainExceptionBase> validationHandler)
    {
        try
        {
            return action();
        }
        catch (DomainExceptionBase validation)
        {
            validationHandler(validation);
            return default;
        }
        catch (Exception ex)
        {
            HandleError(ex);
            return default;
        }
    }

    public static T? Run<T>(Func<T> action)
    {
        return Run(action, NotifyHelper.Warn);
    }

    public static Task RunAsync(Func<Task> action)
    {
        return Task.Run(() => Run(action, DefaultValidationHandler));
    }

    public static Task RunAsync(Func<Task> action, Func<DomainExceptionBase, Task> validationHandler)
    {
        return Task.Run(() => Run(action, validationHandler));
    }

    public static async Task Run(Func<Task> action, Func<DomainExceptionBase, Task> validationHandler)
    {
        try
        {
            await action();
        }
        catch (DomainExceptionBase validation)
        {
            await validationHandler(validation);
        }
        catch (Exception ex)
        {
            HandleError(ex);
        }
    }

    public static Task Run(Func<Task> action)
    {
        return Run(action, DefaultValidationHandler);
    }

    public static async Task<T?> Run<T>(Func<Task<T>> action, Func<DomainExceptionBase, Task> validationHandler)
    {
        try
        {
            return await action();
        }
        catch (DomainExceptionBase validation)
        {
            await validationHandler(validation);
            return default;
        }
        catch (Exception ex)
        {
            HandleError(ex);
            return default;
        }
    }

    public static Task<T?> Run<T>(Func<Task<T>> action)
    {
        return Run(action, DefaultValidationHandler);
    }

    public static Task RunAsync<T>(Func<T, Task> action, T argument)
    {
        return Task.Run(() => Run(action, argument));
    }

    public static async Task Run<T>(Func<T, Task> action, T argument, Action<DomainExceptionBase> validationHandler)
    {
        try
        {
            await action(argument);
        }

        catch (DomainExceptionBase validation)
        {
            validationHandler(validation);
        }
        catch (Exception ex)
        {
            HandleError(ex);
        }
    }

    public static Task Run<T>(Func<T, Task> action, T argument)
    {
        return Run(action, argument, NotifyHelper.Warn);
    }

    static Task DefaultValidationHandler(DomainExceptionBase validation)
    {
        NotifyHelper.Warn(validation);
        return Task.CompletedTask;
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

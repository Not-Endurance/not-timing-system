using System.Diagnostics;
using System.Text;
using Not.Exceptions;
using Not.Logging;
using Not.Notify;

namespace Not.Safe;

// SafeHelper is essentially a centralized error-handler.
// It's the main character in what I'm calling Safe Pattern:
// - Ensures that all backend entry points are error-handled (especially useful in non-web scenarios)
// - public methods are prefixed with "Safe" and changed to private
// - public methods with OG names are added to invoke the Safe methods within SafeHelper.Run
public static class SafeHelper
{
    public static T? Run<T>(Func<T> action, Action<ValidationException> validationHandler)
    {
        try
        {
            return action();
        }
        catch (ValidationException validation)
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
        return Task.Run(() => Run(action, HandleDefaultValidation));
    }

    public static Task RunAsync(Func<Task> action, Func<ValidationException, Task> validationHandler)
    {
        return Task.Run(() => Run(action, validationHandler));
    }

    public static async Task Run(Func<Task> action, Func<ValidationException, Task> validationHandler)
    {
        try
        {
            await action();
        }
        catch (ValidationException validation)
        {
            await validationHandler(validation);
        }
        catch (Exception ex)
        {
            HandleError(ex);
        }
    }

    public static async void Run(Action action, Func<ValidationException, Task> validationHandler)
    {
        try
        {
            action();
        }
        catch (ValidationException validation)
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
        return Run(action, HandleDefaultValidation);
    }

    public static void Run(Action action)
    {
        Run(action, HandleDefaultValidation);
    }

    public static async Task<T?> Run<T>(Func<Task<T>> action, Func<ValidationException, Task> validationHandler)
    {
        try
        {
            return await action();
        }
        catch (ValidationException validation)
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

    public static async Task<IEnumerable<T>> Run<T>(
        Func<Task<IEnumerable<T>>> action,
        Func<ValidationException, Task> validationHandler
    )
    {
        try
        {
            return await action();
        }
        catch (ValidationException validation)
        {
            await validationHandler(validation);
            return [];
        }
        catch (Exception ex)
        {
            HandleError(ex);
            return [];
        }
    }

    public static Task<T?> Run<T>(Func<Task<T>> action)
    {
        return Run(action, HandleDefaultValidation);
    }

    public static Task<IEnumerable<T>> Run<T>(Func<Task<IEnumerable<T>>> action)
    {
        return Run(action, HandleDefaultValidation);
    }

    public static Task RunAsync<T>(Func<T, Task> action, T argument)
    {
        return Task.Run(() => Run(action, argument));
    }

    public static async Task Run<T>(Func<T, Task> action, T argument, Action<ValidationException> validationHandler)
    {
        try
        {
            await action(argument);
        }
        catch (ValidationException validation)
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

    public static async Task HandleExceptionAsync(Exception exception)
    {
        if (exception is ValidationException validation)
        {
            await HandleDefaultValidation(validation);
        }
        else
        {
            HandleError(exception);
        }
    }

    public static void HandleException(Exception exception)
    {
        if (exception is ValidationException validation)
        {
            NotifyHelper.Warn(validation);
        }
        else
        {
            HandleError(exception);
        }
    }

    static Task HandleDefaultValidation(ValidationException validation)
    {
        NotifyHelper.Warn(validation);
        return Task.CompletedTask;
    }

    static void HandleError(Exception ex)
    {
        //#if DEBUG
        //        throw ex;
        //#else
        NotifyHelper.Error(ex);
        var logMessage = $"An error {ex.Message} was thrown at {ex.Source} with trace \n {ex.StackTrace}";
        LoggingHelper.Error(logMessage);
        WriteToTraceConsole(ex);
        //#endif
    }

#pragma warning disable IDE0051 // Used in RELEASE build
    static void WriteToTraceConsole(Exception exception)
#pragma warning restore IDE0051
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

        var message = sb.ToString();
        Trace.WriteLine(message, "console");
    }
}

using System.Globalization;
using System.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Not.Application.Environments;
using Not.Blazor.Components;
using Not.Blazor.Dialogs;
using Not.Localization;
using NTS.Localization;
using NTS.Localization.Resources;

namespace NTS.Tests.Unit.Temporary;

public sealed class ErrorHandlingTests
{
    [Fact]
    public async Task NExceptionHandlerErrorBoundary_logs_captured_exception_as_error_once()
    {
        var logger = new TestLogger<NExceptionHandlerErrorBoundary>();
        var boundary = new TestErrorBoundary { Logger = logger };
        var exception = new InvalidOperationException("boom");

        await boundary.Capture(exception);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(exception, entry.Exception);
        Assert.Contains("Blazor error boundary", entry.Message);
    }

    [Fact]
    public void Dialog_logs_exception_as_error_once()
    {
        var logger = new TestLogger<NExceptionHandlerDialogBehind>();
        var dialog = CreateDialog("Production", logger);
        var exception = new InvalidOperationException("dialog boom");

        dialog.ApplyParameters(exception);
        dialog.ApplyParameters(exception);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(exception, entry.Exception);
        Assert.Contains("exception dialog", entry.Message);
    }

    [Fact]
    public void Environment_registration_exposes_interface_and_concrete_context()
    {
        var services = new ServiceCollection();

        services.AddNEnvironmentContext("Staging");

        using var provider = services.BuildServiceProvider();
        var concrete = provider.GetRequiredService<NEnvironmentContext>();
        var abstraction = provider.GetRequiredService<IEnvironmentContext>();

        Assert.Same(concrete, abstraction);
        Assert.Equal(NEnvironmentNames.STAGING, abstraction.Environment);
    }

    [Fact]
    public void Error_fallback_strings_resolve_for_supported_resources()
    {
        const string key = nameof(NtsStrings.Sorry_we_seem_to_have_fallen_off_the_horseback_string);
        var manager = new ResourceManager(typeof(LocalizedStrings));

        Assert.Equal(
            "Sorry, we seem to have fallen off the horseback",
            manager.GetString(key, CultureInfo.InvariantCulture)
        );
        Assert.False(string.IsNullOrWhiteSpace(manager.GetString(key, CultureInfo.GetCultureInfo("bg"))));
        Assert.False(string.IsNullOrWhiteSpace(manager.GetString(key, CultureInfo.GetCultureInfo("tr"))));
        Assert.False(
            string.IsNullOrWhiteSpace(manager.GetString(nameof(NStrings.Home_string), CultureInfo.GetCultureInfo("tr")))
        );
        Assert.False(
            string.IsNullOrWhiteSpace(
                manager.GetString(nameof(NStrings.Reload_string), CultureInfo.GetCultureInfo("tr"))
            )
        );
        Assert.False(
            string.IsNullOrWhiteSpace(
                manager.GetString(
                    nameof(NStrings.Try_to_Reload_in_order_to_proceed_string),
                    CultureInfo.GetCultureInfo("tr")
                )
            )
        );
    }

    static TestUnhandledExceptionDialog CreateDialog(
        string? environment,
        TestLogger<NExceptionHandlerDialogBehind>? logger = null
    )
    {
        return new TestUnhandledExceptionDialog
        {
            EnvironmentContext = new NEnvironmentContext(environment),
            Logger = logger ?? new TestLogger<NExceptionHandlerDialogBehind>(),
        };
    }

    sealed class TestErrorBoundary : NExceptionHandlerErrorBoundary
    {
        public Task Capture(Exception exception)
        {
            return OnErrorAsync(exception);
        }
    }

    sealed class TestUnhandledExceptionDialog : NExceptionHandlerDialogBehind
    {
        public void ApplyParameters(Exception exception)
        {
            Exception = exception;
            OnParametersSet();
        }
    }

    sealed class TestLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            Entries.Add(new LogEntry(logLevel, exception, formatter(state, exception)));
        }
    }

    sealed class LogEntry
    {
        public LogEntry(LogLevel level, Exception? exception, string message)
        {
            Level = level;
            Exception = exception;
            Message = message;
        }

        public LogLevel Level { get; }
        public Exception? Exception { get; }
        public string Message { get; }
    }
}

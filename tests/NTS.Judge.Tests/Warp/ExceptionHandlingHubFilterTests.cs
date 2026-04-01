using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Not.Notify;
using NTS.Nexus.Warp.Middlewares;

namespace NTS.Judge.Tests.Warp;

public class ExceptionHandlingHubFilterTests
{
    [Fact]
    public async Task InvokeMethodAsync_logs_non_hub_exceptions()
    {
        var notifier = new RecordingNotifier();
        var logger = new RecordingLogger<ExceptionHandlingHubFilter>();
        var filter = new ExceptionHandlingHubFilter(notifier, logger);
        var callerContext = new TestHubCallerContext("judge-connection");
        var hub = new TestHub();
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var invocationContext = new HubInvocationContext(
            callerContext,
            serviceProvider,
            hub,
            typeof(TestHub).GetMethod(nameof(TestHub.Ping))!,
            Array.Empty<object>()
        );

        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                filter
                    .InvokeMethodAsync(
                        invocationContext,
                        _ => ValueTask.FromException<object?>(new InvalidOperationException("boom"))
                    )
                    .AsTask()
        );

        Assert.Single(notifier.Errors);
        Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, logger.Entries[0].LogLevel);
        Assert.IsType<InvalidOperationException>(logger.Entries[0].Exception);
    }

    [Fact]
    public async Task OnConnectedAsync_logs_non_hub_exceptions()
    {
        var notifier = new RecordingNotifier();
        var logger = new RecordingLogger<ExceptionHandlingHubFilter>();
        var filter = new ExceptionHandlingHubFilter(notifier, logger);
        var callerContext = new TestHubCallerContext("judge-connection");
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var lifetimeContext = new HubLifetimeContext(callerContext, serviceProvider, new TestHub());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () =>
                filter.OnConnectedAsync(
                    lifetimeContext,
                    _ => Task.FromException(new InvalidOperationException("connect boom"))
                )
        );

        Assert.Single(notifier.Errors);
        Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, logger.Entries[0].LogLevel);
        Assert.IsType<InvalidOperationException>(logger.Entries[0].Exception);
    }

    sealed class RecordingNotifier : INotifier
    {
        public List<Exception> Errors { get; } = [];

        public void Inform(string message) { }

        public void Success(string message) { }

        public void Warn(string message) { }

        public void Error(string message) { }

        public void Error(Exception ex)
        {
            Errors.Add(ex);
        }
    }

    sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullScope.Instance;
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
            Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
        }
    }

    sealed record LogEntry(LogLevel LogLevel, string Message, Exception? Exception);

    sealed class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new();

        public void Dispose() { }
    }

    sealed class TestHubCallerContext : HubCallerContext
    {
        readonly IDictionary<object, object?> _items = new Dictionary<object, object?>();

        public TestHubCallerContext(string connectionId)
        {
            ConnectionId = connectionId;
            Features = new FeatureCollection();
        }

        public override string ConnectionId { get; }
        public override string? UserIdentifier => "judge";
        public override ClaimsPrincipal User { get; } = new(new ClaimsIdentity());
        public override IDictionary<object, object?> Items => _items;
        public override IFeatureCollection Features { get; }
        public override CancellationToken ConnectionAborted => CancellationToken.None;

        public override void Abort() { }
    }

    sealed class TestHub : Hub
    {
        public Task Ping()
        {
            return Task.CompletedTask;
        }
    }
}

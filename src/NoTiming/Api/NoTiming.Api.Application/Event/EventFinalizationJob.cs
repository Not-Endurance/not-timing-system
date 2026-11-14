using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NoTiming.Api.Application.Event;

// Finalizes events whose last day has ended. It runs at 03:00 and 15:00 UTC, so the afternoon run retries anything the
// morning run could not finish. The job is idempotent, so running it on more than one instance is harmless.
public sealed class EventFinalizationJob : BackgroundService
{
    static readonly TimeSpan[] RunTimes = [TimeSpan.FromHours(3), TimeSpan.FromHours(15)];

    public static DateTimeOffset NextRun(DateTimeOffset now)
    {
        var today = now.UtcDateTime.Date;
        foreach (var time in RunTimes)
        {
            var run = new DateTimeOffset(today + time, TimeSpan.Zero);
            if (run > now)
            {
                return run;
            }
        }
        return new DateTimeOffset(today.AddDays(1) + RunTimes[0], TimeSpan.Zero);
    }

    readonly IServiceScopeFactory _scopes;
    readonly TimeProvider _clock;
    readonly ILogger<EventFinalizationJob> _logger;

    public EventFinalizationJob(IServiceScopeFactory scopes, TimeProvider clock, ILogger<EventFinalizationJob> logger)
    {
        _scopes = scopes;
        _clock = clock;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = _clock.GetUtcNow();
            await Task.Delay(NextRun(now) - now, _clock, stoppingToken);
            await FinalizeDueEvents(stoppingToken);
        }
    }

    async Task FinalizeDueEvents(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var businessService = scope.ServiceProvider.GetRequiredService<IEventInformationBusinessService>();
            await businessService.FinalizeDueEvents();
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogError(ex, "Finalizing due events failed. It will be retried at the next run.");
        }
    }
}

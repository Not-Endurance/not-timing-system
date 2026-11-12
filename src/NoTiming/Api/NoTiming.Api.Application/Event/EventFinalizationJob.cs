using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NoTiming.Api.Application.Event;

// Finalizes events whose last day has ended. It runs every night at 03:00 in Europe/Sofia, the time zone of the
// Bulgarian events. The job is idempotent, so running it on more than one instance is harmless.
public sealed class EventFinalizationJob : BackgroundService
{
    static readonly TimeZoneInfo RunZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Sofia");
    static readonly TimeSpan RunTime = TimeSpan.FromHours(3);

    public static DateTimeOffset NextRun(DateTimeOffset now)
    {
        var local = TimeZoneInfo.ConvertTime(now, RunZone);
        var target = local.Date + RunTime;
        if (local.DateTime >= target)
        {
            target = target.AddDays(1);
        }
        return new DateTimeOffset(target, RunZone.GetUtcOffset(target));
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
            var delay = NextRun(_clock.GetUtcNow()) - _clock.GetUtcNow();
            await Task.Delay(delay, _clock, stoppingToken);
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

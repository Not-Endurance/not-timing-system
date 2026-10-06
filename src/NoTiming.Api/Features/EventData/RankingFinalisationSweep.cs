using Microsoft.Extensions.Options;

namespace NoTiming.Api.Features.EventData;

/// <summary>The setting <c>RankingFinalisation:Enabled</c>: the sweep runs unless it is turned off.</summary>
internal sealed class RankingFinalisationOptions
{
    public const string SECTION = "RankingFinalisation";

    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Nothing fires at the end of the last day of an Event (ADR-0007), so the host looks (#640, ADR-0006): when it starts and
/// then every hour by the clock it was given, it finalises the Rankings of every Event that has ended and has no stored
/// placings. Until it has run readers compose the placings in memory, so nothing waits for it, and a run that fails is logged
/// and tried again an hour later: it never stops the host.
/// </summary>
internal sealed class RankingFinalisationSweep : BackgroundService
{
    static readonly TimeSpan INTERVAL = TimeSpan.FromHours(1);

    readonly RankingFinaliser _finaliser;
    readonly TimeProvider _time;
    readonly IOptions<RankingFinalisationOptions> _options;
    readonly ILogger<RankingFinalisationSweep> _log;

    public RankingFinalisationSweep(
        RankingFinaliser finaliser,
        TimeProvider time,
        IOptions<RankingFinalisationOptions> options,
        ILogger<RankingFinalisationSweep> log
    )
    {
        _finaliser = finaliser;
        _time = time;
        _options = options;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Value.Enabled)
        {
            _log.LogInformation("The finalisation of Rankings is turned off.");
            return;
        }

        using var timer = new PeriodicTimer(INTERVAL, _time);
        try
        {
            await RunAsync(stoppingToken);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // the host is stopping
        }
    }

    async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            var report = await _finaliser.RunAsync(cancellationToken);
            if (report.Finalised > 0)
            {
                _log.LogInformation("Finalised {Finalised} Rankings.", report.Finalised);
            }
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            _log.LogError(ex, "The finalisation of Rankings failed; it is tried again in an hour.");
        }
    }
}

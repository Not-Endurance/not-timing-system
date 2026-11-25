using Microsoft.AspNetCore.Mvc;
using NoTiming.Api.Application.Features.Snapshots;
using NoTiming.Api.Application.Logging;
using NoTiming.Api.Application.Telemetry;
using NoTiming.Api.Base;
using NoTiming.Application.Contracts.Watcher.Models;

namespace NoTiming.Api.Controllers;

/// <summary>A judge's own snapshots, applied like a witness's and returned with the participations they changed.</summary>
public class JudgeSnapshotController : ApiControllerBase
{
    readonly SnapshotApplicationService _snapshots;

    public JudgeSnapshotController(
        IFunctionLogger<JudgeSnapshotController> logger,
        ITelemetryService telemetry,
        SnapshotApplicationService snapshots
    )
        : base(logger, telemetry)
    {
        _snapshots = snapshots;
    }

    [HttpPost("participations/{eventId:int}/snapshots")]
    public async Task<IActionResult> Record(int eventId)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Record));
        TagRequest(request);
        LogInformation(request, nameof(Record));
        var group = await ReadBody<SnapshotGroupModel>(request);
        return Ok(await _snapshots.ApplyFromJudge(eventId, group));
    }
}

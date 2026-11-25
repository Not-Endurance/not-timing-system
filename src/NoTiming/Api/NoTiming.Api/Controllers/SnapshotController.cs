using Microsoft.AspNetCore.Mvc;
using NoTiming.Api.Application.Features.Snapshots;
using NoTiming.Api.Application.Logging;
using NoTiming.Api.Application.Telemetry;
using NoTiming.Api.Base;
using NoTiming.Application.Contracts.Watcher.Models;

namespace NoTiming.Api.Controllers;

/// <summary>Witness snapshots. The API applies them to participations and announces each applied change.</summary>
public class SnapshotController : ApiControllerBase
{
    readonly SnapshotApplicationService _snapshots;

    public SnapshotController(
        IFunctionLogger<SnapshotController> logger,
        ITelemetryService telemetry,
        SnapshotApplicationService snapshots
    )
        : base(logger, telemetry)
    {
        _snapshots = snapshots;
    }

    [HttpPost("snapshots/{eventId:int}")]
    public async Task<IActionResult> Apply(int eventId)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Apply));
        TagRequest(request);
        LogInformation(request, nameof(Apply));
        var group = await ReadBody<SnapshotGroupModel>(request);
        await _snapshots.ApplyFromWitness(User, eventId, group);
        return Ok();
    }
}

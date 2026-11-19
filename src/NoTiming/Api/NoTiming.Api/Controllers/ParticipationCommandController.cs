using Microsoft.AspNetCore.Mvc;
using NoTiming.Api.Application.Features.Participations;
using NoTiming.Api.Application.Logging;
using NoTiming.Api.Application.Telemetry;
using NoTiming.Api.Base;
using NoTiming.Application.Contracts.Core;
using NoTiming.Application.Contracts.Core.Commands;

namespace NoTiming.Api.Controllers;

/// <summary>Judge commands. Each one runs its rule in the API and returns the new state of the participation.</summary>
public class ParticipationCommandController : ApiControllerBase
{
    readonly ParticipationCommandService _commands;

    public ParticipationCommandController(
        IFunctionLogger<ParticipationCommandController> logger,
        ITelemetryService telemetry,
        ParticipationCommandService commands
    )
        : base(logger, telemetry)
    {
        _commands = commands;
    }

    [HttpPost("participations/{eventId:int}/phase")]
    public async Task<IActionResult> UpdatePhase(int eventId)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(UpdatePhase));
        TagRequest(request);
        LogInformation(request, nameof(UpdatePhase));
        var model = await ReadBody<PhaseUpdateModel>(request);
        return Ok(await _commands.UpdatePhase(eventId, model));
    }

    [HttpPost("participations/{eventId:int}/{participationId:int}/representation")]
    public async Task<IActionResult> SetRepresentation(int eventId, int participationId)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(SetRepresentation));
        TagRequest(request);
        LogInformation(request, nameof(SetRepresentation));
        var model = await ReadBody<ToggleModel>(request);
        return Ok(await _commands.SetRepresentation(eventId, participationId, model.IsRequested));
    }

    [HttpPost("participations/{eventId:int}/{participationId:int}/inspection")]
    public async Task<IActionResult> SetInspection(int eventId, int participationId)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(SetInspection));
        TagRequest(request);
        LogInformation(request, nameof(SetInspection));
        var model = await ReadBody<ToggleModel>(request);
        return Ok(await _commands.SetInspection(eventId, participationId, model.IsRequested));
    }

    [HttpPost("participations/{eventId:int}/{participationId:int}/withdraw")]
    public async Task<IActionResult> Withdraw(int eventId, int participationId)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Withdraw));
        TagRequest(request);
        LogInformation(request, nameof(Withdraw));
        return Ok(await _commands.Withdraw(eventId, participationId));
    }

    [HttpPost("participations/{eventId:int}/{participationId:int}/retire")]
    public async Task<IActionResult> Retire(int eventId, int participationId)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Retire));
        TagRequest(request);
        LogInformation(request, nameof(Retire));
        return Ok(await _commands.Retire(eventId, participationId));
    }

    [HttpPost("participations/{eventId:int}/{participationId:int}/finish-not-ranked")]
    public async Task<IActionResult> FinishNotRanked(int eventId, int participationId)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(FinishNotRanked));
        TagRequest(request);
        LogInformation(request, nameof(FinishNotRanked));
        var model = await ReadBody<FinishNotRankedModel>(request);
        return Ok(await _commands.FinishNotRanked(eventId, participationId, model.Reason));
    }

    [HttpPost("participations/{eventId:int}/{participationId:int}/disqualify")]
    public async Task<IActionResult> Disqualify(int eventId, int participationId)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Disqualify));
        TagRequest(request);
        LogInformation(request, nameof(Disqualify));
        var model = await ReadBody<DisqualifyModel>(request);
        return Ok(await _commands.Disqualify(eventId, participationId, model.Codes, model.Reason));
    }

    [HttpPost("participations/{eventId:int}/{participationId:int}/fail-to-qualify")]
    public async Task<IActionResult> FailToQualify(int eventId, int participationId)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(FailToQualify));
        TagRequest(request);
        LogInformation(request, nameof(FailToQualify));
        var model = await ReadBody<FailToQualifyModel>(request);
        return Ok(await _commands.FailToQualify(eventId, participationId, model.Codes, model.Reason));
    }

    [HttpPost("participations/{eventId:int}/{participationId:int}/restore")]
    public async Task<IActionResult> RestoreQualification(int eventId, int participationId)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(RestoreQualification));
        TagRequest(request);
        LogInformation(request, nameof(RestoreQualification));
        return Ok(await _commands.RestoreQualification(eventId, participationId));
    }
}

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Not.Application.CRUD.Ports;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Core.Models;
using NTS.Nexus.Warp.Api.Base;
using NTS.Nexus.Warp.Api.Logging;
using NTS.Nexus.Warp.Api.Telemetry;

namespace NTS.Nexus.Warp.Api.Event;

public class EventInformationController : ApiControllerBase
{
    readonly IRepository<EventInformationModel> _events;
    readonly IEventInformationResetService _resetService;
    readonly IEventInformationBusinessService _businessService;

    public EventInformationController(
        IFunctionLogger<EventInformationController> logger,
        IRepository<EventInformationModel> events,
        IEventInformationResetService resetService,
        IEventInformationBusinessService businessService,
        ITelemetryService telemetry
    )
        : base(logger, telemetry)
    {
        _events = events;
        _resetService = resetService;
        _businessService = businessService;
    }

    [HttpPost("api/event")]
    public async Task<IActionResult> Create()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Create));
        TagRequest(request);
        LogInformation(request, nameof(Create));

        var payload = await ReadBody<EventInformationModel>(request);
        await _events.Create(payload);
        return Ok();
    }

    [HttpPatch("api/event")]
    public async Task<IActionResult> Update()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Update));
        TagRequest(request);
        LogInformation(request, nameof(Update));

        var payload = await ReadBody<EventInformationModel>(request);
        await _events.Update(payload);
        return Ok();
    }

    [HttpGet("api/event/{id:int}")]
    public async Task<IActionResult> Read(int id)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Read));
        TagRequest(request);
        LogInformation(request, nameof(Read));

        return Ok(await _events.Read(x => x.Id == id));
    }

    [HttpGet("api/event")]
    public async Task<IActionResult> List()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(List));
        TagRequest(request);
        LogInformation(request, nameof(List));

        return Ok(await _events.ReadMany() ?? []);
    }

    [HttpGet("api/event/active")]
    public async Task<IActionResult> ListActive()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(ListActive));
        TagRequest(request);
        LogInformation(request, nameof(ListActive));

        return Ok(await _businessService.ReadActive());
    }

    [HttpGet("api/event/past")]
    public async Task<IActionResult> ListPast()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(ListPast));
        TagRequest(request);
        LogInformation(request, nameof(ListPast));

        var events = await _businessService.ReadPast();
        return Ok(events);
    }

    [HttpDelete("api/event/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Delete));
        TagRequest(request);
        LogInformation(request, nameof(Delete));

        var document = await _events.Read(x => x.Id == id);
        if (document == null)
        {
            return Ok();
        }

        await _events.DeleteMany(x => x.Id == id);
        return Ok();
    }

    [HttpDelete("api/event/{id:int}/reset")]
    public async Task<IActionResult> Reset(int id)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Reset));
        TagRequest(request);
        LogInformation(request, nameof(Reset));

        await _resetService.Reset(id);
        return Ok();
    }

    [HttpPost("api/event/{id:int}/deactivate")]
    public async Task<IActionResult> Deactivate(int id)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Deactivate));
        TagRequest(request);
        LogInformation(request, nameof(Deactivate));

        await _businessService.Deactivate(id);
        return Ok();
    }

    [HttpPost("api/event/{id:int}/start")]
    public async Task<IActionResult> Start(int id)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Start));
        TagRequest(request);
        LogInformation(request, nameof(Start));

        return Ok(await _businessService.Start(id));
    }
}

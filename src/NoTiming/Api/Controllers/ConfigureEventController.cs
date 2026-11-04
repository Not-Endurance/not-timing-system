using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Not.Application.CRUD.Ports;
using NTS.Application.Contracts.Setup;
using NTS.Application.Contracts.Setup.Models;
using NoTiming.Api.Base;
using NoTiming.Api.Logging;
using NoTiming.Api.Telemetry;

namespace NoTiming.Api.Controllers;

public class ConfigureEventController : ApiControllerBase
{
    readonly IRepository<ConfigureEventModel> _configureEvents;
    readonly IConfigureEventMutationGuard _mutationGuard;

    public ConfigureEventController(
        IRepository<ConfigureEventModel> configureEventRepository,
        IConfigureEventMutationGuard mutationGuard,
        IFunctionLogger<ConfigureEventController> logger,
        ITelemetryService telemetry
    )
        : base(logger, telemetry)
    {
        _configureEvents = configureEventRepository;
        _mutationGuard = mutationGuard;
    }

    [HttpPost("configure-event")]
    public async Task<IActionResult> Insert()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Insert));
        TagRequest(request);
        LogInformation(request, nameof(Insert));

        var document = await ReadBody<ConfigureEventModel>(request);
        await _configureEvents.Create(document);
        return Ok();
    }

    [HttpGet("configure-event")]
    public async Task<IActionResult> List()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(List));
        TagRequest(request);
        LogInformation(request, nameof(List));

        return Ok(await _configureEvents.ReadMany() ?? []);
    }

    [HttpGet("configure-event/{id:int}")]
    public async Task<IActionResult> QueryById(int id)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(QueryById));
        TagRequest(request);
        LogInformation(request, nameof(QueryById));

        return Ok(await _configureEvents.Read(id));
    }

    [HttpPatch("configure-event")]
    public async Task<IActionResult> Update()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Update));
        TagRequest(request);
        LogInformation(request, nameof(Update));

        var document = await ReadBody<ConfigureEventModel>(request);
        await _mutationGuard.EnsureCanMutate(document.Id);
        await _configureEvents.Update(document);
        return Ok();
    }

    [HttpDelete("configure-event/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Delete));
        TagRequest(request);
        LogInformation(request, nameof(Delete));

        await _mutationGuard.EnsureCanMutate(id);
        var configureEvent = await _configureEvents.Read(id);
        if (configureEvent == null)
        {
            return Ok();
        }

        await _configureEvents.Delete(configureEvent);
        return Ok();
    }

    [HttpDelete("configure-event")]
    public async Task<IActionResult> DeleteMany()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(DeleteMany));
        TagRequest(request);
        LogInformation(request, nameof(DeleteMany));

        var configureEvents = await ReadBody<ConfigureEventModel[]>(request);
        await _mutationGuard.EnsureCanMutate(configureEvents.Select(x => x.Id));
        await _configureEvents.DeleteMany(configureEvents);
        return Ok();
    }
}

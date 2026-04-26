using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Not.Application.CRUD.Ports;
using NTS.Application.Contracts.Shared;
using NTS.Application.Contracts.Shared.Models;
using NTS.Nexus.HTTP.Logger;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Functions.Base;

public class EventScopedCrudFunctions<T> : FunctionBase
    where T : class, IEventScopedDocument
{
    readonly IRepository<T> _repository;

    public EventScopedCrudFunctions(
        IFunctionLogger<EventScopedCrudFunctions<T>> logger,
        IRepository<T> repository,
        ITelemetryService telemetry
    )
        : base(logger, telemetry)
    {
        _repository = repository;
    }

    protected async Task<IActionResult> InternalCreate(HttpRequest request, int eventId)
    {
        var payload = await ReadBody<T>(request);
        payload.EventId = eventId;
        await _repository.Create(payload);
        return Ok();
    }

    protected async Task<IActionResult> InternalRead(HttpRequest _, int eventId, int id)
    {
        return Ok(await _repository.Read(x => x.EventId == eventId && x.Id == id));
    }

    protected async Task<IActionResult> InternalReadMany(HttpRequest _, int eventId)
    {
        return Ok(await _repository.ReadMany(x => x.EventId == eventId) ?? []);
    }

    protected async Task<IActionResult> InternalUpdate(HttpRequest request, int eventId)
    {
        var payload = await ReadBody<T>(request);
        payload.EventId = eventId;
        await _repository.Update(payload);
        return Ok();
    }

    protected async Task<IActionResult> InternalDelete(HttpRequest _, int eventId, int id)
    {
        await _repository.Delete(x => x.EventId == eventId && x.Id == id);
        return Ok();
    }
}

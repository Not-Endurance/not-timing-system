using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Not.Application.CRUD.Ports;
using NTS.Application.Shared;
using NTS.Nexus.HTTP.Logger;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Functions.Base;

public class EventScopedCrudFunctions<T> : FunctionBase
    where T : class, IEventScopedDocument, ISoftDeletableDocument
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
        if (payload == null)
        {
            return UnexpectedPayload<T>();
        }

        payload.EventId = eventId;
        await _repository.Create(payload);
        return Ok();
    }

    protected async Task<IActionResult> InternalRead(HttpRequest _, int eventId, int id)
    {
        var result = await _repository.Read(x => x.EventId == eventId && x.Id == id);
        if (result == null)
        {
            return new NotFoundResult();
        }

        return Ok(result);
    }

    protected async Task<IActionResult> InternalReadMany(HttpRequest _, int eventId)
    {
        var result = await _repository.ReadMany(x => x.EventId == eventId);
        return Ok(result);
    }

    protected async Task<IActionResult> InternalUpdate(HttpRequest request, int eventId)
    {
        var payload = await ReadBody<T>(request);
        if (payload == null)
        {
            return UnexpectedPayload<T>();
        }

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

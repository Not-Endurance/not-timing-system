using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Not.Application.CRUD.Ports;
using NTS.Nexus.HTTP.Logger;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Functions.Base;

public class CrudFunctions<T> : FunctionBase
    where T : class
{
    readonly IRepository<T> _repository;

    public CrudFunctions(
        IFunctionLogger<CrudFunctions<T>> logger,
        IRepository<T> repository,
        ITelemetryService telemetry
    )
        : base(logger, telemetry)
    {
        _repository = repository;
    }

    protected async Task<IActionResult> InternalCreate(HttpRequest request)
    {
        var payload = await ReadBody<T>(request);
        await _repository.Create(payload);
        return Ok();
    }

    protected async Task<IActionResult> InternalRead(HttpRequest _, int id)
    {
        return Ok(await _repository.Read(id));
    }

    protected async Task<IActionResult> InternalReadMany(HttpRequest _)
    {
        return Ok(await _repository.ReadMany() ?? []);
    }

    protected async Task<IActionResult> InternalUpdate(HttpRequest request)
    {
        var payload = await ReadBody<T>(request);
        await _repository.Update(payload);
        return Ok();
    }

    protected async Task<IActionResult> InternalDelete(HttpRequest _, int id)
    {
        var result = await _repository.Read(id);
        if (result == null)
        {
            return Ok();
        }

        await _repository.Delete(result);
        return Ok();
    }
}

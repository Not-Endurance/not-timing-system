using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.OData;
using Not.Storage.Mongo;
using NTS.Nexus.HTTP.Logger;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Functions.Base;

public class CrudFunctions<T> : FunctionBase
    where T : class
{
    readonly IMongoRepository<T> _repository;

    public CrudFunctions(
        IFunctionLogger<CrudFunctions<T>> logger,
        IMongoRepository<T> repository,
        ITelemetryService telemetry
    )
        : base(logger, telemetry)
    {
        _repository = repository;
    }

    protected IMongoRepository<T> Repository => _repository;

    protected async Task<IActionResult> CreateCore(HttpRequest request)
    {
        var payload = await ReadBody<T>(request);
        await _repository.Create(payload);
        return Ok();
    }

    protected async Task<IActionResult> ReadCore(int id)
    {
        return Ok(await _repository.Read(id));
    }

    protected async Task<IActionResult> ReadManyCore(HttpRequest request)
    {
        if (!request.QueryString.HasValue)
        {
            return Ok(await _repository.ReadMany() ?? []);
        }

        if (!TryCreateOptions(request, out var options, out var badRequest))
        {
            return badRequest;
        }

        return Ok(await _repository.ReadMany(options) ?? []);
    }

    protected async Task<IActionResult> UpdateCore(HttpRequest request)
    {
        var payload = await ReadBody<T>(request);
        await _repository.Update(payload);
        return Ok();
    }

    protected async Task<IActionResult> DeleteCore(int id)
    {
        await _repository.Delete(id);
        return Ok();
    }

    protected async Task<IActionResult> DeleteManyCore(HttpRequest request)
    {
        var payload = await ReadBody<T[]>(request);
        await _repository.DeleteMany(payload);
        return Ok();
    }

    static bool TryCreateOptions(HttpRequest request, out ODataQueryOptions<T> options, out IActionResult badRequest)
    {
        try
        {
            options = ODataQueryOptionsFactory.Create<T>(request);
            badRequest = default!;
            return true;
        }
        catch (Exception ex) when (IsODataQueryException(ex))
        {
            options = default!;
            badRequest = new BadRequestObjectResult(ex.Message);
            return false;
        }
    }

    static bool IsODataQueryException(Exception exception)
    {
        return exception is ODataException or ArgumentException or InvalidOperationException;
    }
}

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

    protected Task<IActionResult> InternalCreate(HttpRequest request)
    {
        return ExecuteWithTelemetry(nameof(InternalCreate), async () =>
        {
            var payload = await ReadBody<T>(request);
            if (payload == null)
            {
                return UnexpectedPayload<T>();
            }
            await ExecuteWithTelemetry("RepositoryCreate", () => _repository.Create(payload));
            return Ok();
        });
    }

    protected Task<IActionResult> InternalRead(HttpRequest request, int id)
    {
        return ExecuteWithTelemetry(nameof(InternalRead), async () =>
        {
            var result = await ExecuteWithTelemetry("RepositoryRead", () => _repository.Read(id));
            if (result == null)
            {
                return new NotFoundResult();
            }
            return Ok(result);
        });
    }

    protected Task<IActionResult> InternalReadMany(HttpRequest request)
    {
        return ExecuteWithTelemetry(nameof(InternalReadMany), async () =>
        {
            var result = await ExecuteWithTelemetry("RepositoryReadMany", () => _repository.ReadMany());
            return Ok(result);
        });
    }

    protected Task<IActionResult> InternalUpdate(HttpRequest request)
    {
        return ExecuteWithTelemetry(nameof(InternalUpdate), async () =>
        {
            var payload = await ReadBody<T>(request);
            if (payload == null)
            {
                return UnexpectedPayload<T>();
            }
            await ExecuteWithTelemetry("RepositoryUpdate", () => _repository.Update(payload));
            return Ok();
        });
    }

    protected Task<IActionResult> InternalDelete(HttpRequest request, int id)
    {
        return ExecuteWithTelemetry(nameof(InternalDelete), async () =>
        {
            var result = await ExecuteWithTelemetry("RepositoryRead", () => _repository.Read(id));
            if (result == null)
            {
                return Ok();
            }
            await ExecuteWithTelemetry("RepositoryDelete", () => _repository.Delete(result));
            return Ok();
        });
    }
}

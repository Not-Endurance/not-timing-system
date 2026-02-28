using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Not.Application.CRUD.Ports;
using NTS.Nexus.HTTP.Logger;

namespace NTS.Nexus.HTTP.Functions.Base;

public class CrudFunctions<T> : FunctionBase
    where T : class
{
    readonly IRepository<T> _repository;

    public CrudFunctions(IFunctionLogger<CrudFunctions<T>> logger, IRepository<T> repository)
        : base(logger)
    {
        _repository = repository;
    }

    protected async Task<IActionResult> InternalCreate(HttpRequest request)
    {
        LogInformation(request);

        var payload = await ReadBody<T>(request);
        if (payload == null)
        {
            return UnexpectedPayload<T>();
        }
        await _repository.Create(payload);
        return Ok();
    }

    protected async Task<IActionResult> InternalRead(HttpRequest request, int id)
    {
        LogInformation(request);

        var result = await _repository.Read(id);
        if (result == null)
        {
            return new NotFoundResult();
        }
        return Ok(result);
    }

    protected async Task<IActionResult> InternalReadMany(HttpRequest request)
    {
        LogInformation(request);

        var result = await _repository.ReadMany();
        return Ok(result);
    }

    protected async Task<IActionResult> InternalUpdate(HttpRequest request)
    {
        LogInformation(request);

        var payload = await ReadBody<T>(request);
        if (payload == null)
        {
            return UnexpectedPayload<T>();
        }
        await _repository.Update(payload);
        return Ok();
    }

    protected async Task<IActionResult> InternalDelete(HttpRequest request, int id)
    {
        LogInformation(request);

        var result = await _repository.Read(id);
        if (result == null)
        {
            return Ok();
        }
        await _repository.Delete(result);
        return Ok();
    }
}

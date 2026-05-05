using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Not.Application.CRUD.Ports;
using NTS.Application.Contracts.Setup;
using NTS.Application.Contracts.Setup.Models;
using NTS.Nexus.HTTP.Functions.Base;
using NTS.Nexus.HTTP.Logger;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Functions;

public class HorseFunctions : FunctionBase
{
    readonly IRepository<HorseModel> _horses;

    public HorseFunctions(
        IFunctionLogger<HorseFunctions> logger,
        IRepository<HorseModel> horses,
        ITelemetryService telemetry
    )
        : base(logger, telemetry)
    {
        _horses = horses;
    }

    [Function("horses-insert")]
    public async Task<IActionResult> Insert(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "horses")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(Insert));
        TagRequest(request);
        LogInformation(request, nameof(Insert));

        var document = await ReadBody<HorseModel>(request);
        await _horses.Create(document);
        return Ok();
    }

    [Function("horses-update")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "horses")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(Update));
        TagRequest(request);
        LogInformation(request, nameof(Update));

        var document = await ReadBody<HorseModel>(request);
        await _horses.Update(document);
        return Ok();
    }

    [Function("horses-safe-delete")]
    public async Task<IActionResult> SafeDelete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "horses/{id:int}/safe")] HttpRequest request,
        int id
    )
    {
        using var activity = StartFunctionActivity(nameof(SafeDelete));
        TagRequest(request);
        LogInformation(request, nameof(SafeDelete));

        var horse = await _horses.Read(id);
        if (horse == null)
        {
            return Ok();
        }

        await _horses.Delete(horse);
        return Ok();
    }

    [Function("horses-delete")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "horses/{id:int}")] HttpRequest request,
        int id
    )
    {
        using var activity = StartFunctionActivity(nameof(Delete));
        TagRequest(request);
        LogInformation(request, nameof(Delete));

        var horse = await _horses.Read(id);
        if (horse == null)
        {
            return Ok();
        }

        await _horses.Delete(horse);
        return Ok();
    }

    [Function("horses-delete-many")]
    public async Task<IActionResult> DeleteMany(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "horses")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(DeleteMany));
        TagRequest(request);
        LogInformation(request, nameof(DeleteMany));

        var horses = await ReadBody<HorseModel[]>(request);
        await _horses.DeleteMany(horses);
        return Ok();
    }

    [Function("horses-get")]
    public async Task<IActionResult> GetOne(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "horses/{id:int}")] HttpRequest request,
        int id
    )
    {
        using var activity = StartFunctionActivity(nameof(GetOne));
        TagRequest(request);
        LogInformation(request, nameof(GetOne));

        return Ok(await _horses.Read(id));
    }

    [Function("horses-list")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "horses")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(List));
        TagRequest(request);
        LogInformation(request, nameof(List));

        return Ok(await _horses.ReadMany() ?? []);
    }
}

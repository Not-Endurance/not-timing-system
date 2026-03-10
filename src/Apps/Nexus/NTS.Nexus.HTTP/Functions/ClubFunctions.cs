using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Not.Application.CRUD.Ports;
using Not.Async;
using Not.Async.Extensions;
using NTS.Application.Setup;
using NTS.Domain.Setup.Aggregates;
using NTS.Nexus.HTTP.Functions.Base;
using NTS.Nexus.HTTP.Logger;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Functions;

public class ClubFunctions : FunctionBase
{
    readonly IRepository<ClubModel> _clubs;

    public ClubFunctions(
        IFunctionLogger<ClubFunctions> logger,
        IRepository<ClubModel> clubs,
        ITelemetryService telemetry
    )
        : base(logger, telemetry)
    {
        _clubs = clubs;
    }

    [Function("clubs-insert")]
    public async Task<IActionResult> Insert(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "clubs")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(Insert));
        TagRequest(request);
        LogInformation(request, nameof(Insert));

        var club = await ReadBody<Club>(request);
        if (club == null)
        {
            return UnexpectedPayload<Club>();
        }

        var document = ClubModel.From(club);
        await _clubs.Create(document);
        return new OkObjectResult($"Inserted {club}");
    }

    [Function("clubs-update")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "clubs")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(Update));
        TagRequest(request);
        LogInformation(request, nameof(Update));

        var club = await ReadBody<Club>(request);
        if (club == null)
        {
            return UnexpectedPayload<Club>();
        }

        var document = ClubModel.From(club);
        await _clubs.Update(document);
        return new OkObjectResult($"Updated {club}");
    }

    [Function("clubs-delete")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "clubs/{id:int}")] HttpRequest request,
        int id
    )
    {
        using var activity = StartFunctionActivity(nameof(Delete));
        TagRequest(request);
        LogInformation(request, nameof(Delete));

        var club = await _clubs.Read(id);
        if (club == null)
        {
            return new OkObjectResult($"Club wiht id '{id}' did not exist");
        }

        await _clubs.Delete(club);
        return new OkObjectResult($"Deleted club with id '{id}'");
    }

    [Function("clubs-get-one")]
    public async Task<IActionResult> GetOne(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "clubs/{id:int}")] HttpRequest request,
        int id
    )
    {
        using var activity = StartFunctionActivity(nameof(GetOne));
        TagRequest(request);
        LogInformation(request, nameof(GetOne));

        var club = await _clubs.Read(id);
        return new OkObjectResult(club?.MapToEntity());
    }

    [Function("clubs-list")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "clubs")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(List));
        TagRequest(request);
        LogInformation(request, nameof(List));

        var clubs = await _clubs.ReadMany().Select(x => x.MapToEntity());
        return new OkObjectResult(clubs);
    }
}

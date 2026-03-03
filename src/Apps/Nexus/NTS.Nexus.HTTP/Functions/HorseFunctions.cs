using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Not.Application.CRUD.Ports;
using Not.Async.Extensions;
using NTS.Application.Setup;
using NTS.Domain.Setup.Aggregates;
using NTS.Nexus.HTTP.Functions.Base;
using NTS.Nexus.HTTP.Logger;
using NTS.Nexus.HTTP.Mongo.Repositories;

namespace NTS.Nexus.HTTP.Functions;

public class HorseFunctions : FunctionBase
{
    readonly IRepository<HorseModel> _horses;
    readonly IArchiveRepository _archive;

    public HorseFunctions(
        IFunctionLogger<HorseFunctions> logger,
        IRepository<HorseModel> horses,
        IArchiveRepository archive
    )
        : base(logger)
    {
        _horses = horses;
        _archive = archive;
    }

    [Function("horses-insert")]
    public async Task<IActionResult> Insert(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "horses")] HttpRequest request
    )
    {
        TagRequest(request);
        LogInformation(request, nameof(Insert));

        var horse = await ReadBody<Horse>(request);
        if (horse == null)
        {
            return UnexpectedPayload<Horse>();
        }

        var document = HorseModel.MapFrom(horse);
        await _horses.Create(document);
        return new OkObjectResult($"Inserted {horse}");
    }

    [Function("horses-update")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "horses")] HttpRequest request
    )
    {
        TagRequest(request);
        LogInformation(request, nameof(Update));

        var horse = await ReadBody<Horse>(request);
        if (horse == null)
        {
            return UnexpectedPayload<Horse>();
        }

        var document = HorseModel.MapFrom(horse);
        await _horses.Update(document);
        return new OkObjectResult($"Updated {horse}");
    }

    [Function("horses-safe-delete")]
    public async Task<IActionResult> SafeDelete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "horses/{id:int}/safe")] HttpRequest request,
        int id
    )
    {
        TagRequest(request);
        LogInformation(request, nameof(SafeDelete));

        var recordsWithHorse = await _archive
            .ReadMany(x =>
                x.Ranklists.Any(y => y.Entries.Any(z => z.Participation.Combination.Horse.Id == id))
            )
            .ToList();

        if (recordsWithHorse.Any())
        {
            return new OkObjectResult(
                $"The horse you want to delete has participated in '{recordsWithHorse.Count}' events. It will not be removed from those archives, but will no longer be visible for future events"
            );
        }

        var horse = await _horses.Read(id);
        if (horse == null)
        {
            return new OkObjectResult($"Club with id '{id}' did not exist");
        }

        await _horses.Delete(horse);
        return new OkObjectResult($"Deleted horse with id '{id}'");
    }

    [Function("horses-delete")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "horses/{id:int}")] HttpRequest request,
        int id
    )
    {
        TagRequest(request);
        LogInformation(request, nameof(Delete));

        var horse = await _horses.Read(id);
        if (horse == null)
        {
            return new OkObjectResult($"Horse wiht id '{id}' did not exist");
        }

        await _horses.Delete(horse);
        return new OkObjectResult($"Deleted horse with id '{id}'");
    }

    [Function("horses-get")]
    public async Task<IActionResult> GetOne(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "horses/{id:int}")] HttpRequest request,
        int id
    )
    {
        TagRequest(request);
        LogInformation(request, nameof(GetOne));

        var horse = await _horses.Read(id);
        return new OkObjectResult(horse);
    }

    [Function("horses-list")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "horses")] HttpRequest request
    )
    {
        TagRequest(request);
        LogInformation(request, nameof(List));

        var horses = await _horses.ReadMany().Select(x => x.MaptoDomain());
        return new OkObjectResult(horses);
    }
}

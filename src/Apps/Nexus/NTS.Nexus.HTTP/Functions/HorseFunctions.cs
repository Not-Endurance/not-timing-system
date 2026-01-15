using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Not.Application.CRUD.Ports;
using Not.Concurrency.Extensions;
using Not.Serialization.JSON;
using NTS.Application.Models;
using NTS.Domain.Setup.Aggregates;
using NTS.Nexus.HTTP.Functions.Base;
using NTS.Nexus.HTTP.Logger;
using NTS.Storage.Mongo.Repositories;

namespace NTS.Nexus.HTTP.Functions;

public class HorseFunctions : FunctionBase<HorseFunctions>
{
    readonly IRepository<SetupHorseModel> _horses;
    readonly IArchiveMongoRepository _archive;

    public HorseFunctions(
        IFunctionLogger<HorseFunctions> logger,
        IRepository<SetupHorseModel> horses,
        IArchiveMongoRepository archive
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
        LogInformation(request);

        var requestBody = await new StreamReader(request.Body).ReadToEndAsync();
        var horse = requestBody.FromJson<Horse>();
        var document = SetupHorseModel.MapFrom(horse);
        await _horses.Create(document);

        return new OkObjectResult($"Inserted {horse}");
    }

    [Function("horses-update")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "horses")] HttpRequest request
    )
    {
        LogInformation(request);

        var requestBody = await new StreamReader(request.Body).ReadToEndAsync();
        var horse = requestBody.FromJson<Horse>();
        var document = SetupHorseModel.MapFrom(horse);
        await _horses.Update(document);

        return new OkObjectResult($"Updated {horse}");
    }

    [Function("horses-safe-delete")]
    public async Task<IActionResult> SafeDelete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "horses/{id:int}/safe")] HttpRequest request,
        int id
    )
    {
        LogInformation(request);

        var requestBody = await new StreamReader(request.Body).ReadToEndAsync();

        var recordsWithHorse = await _archive
            .ReadAll(x => x.Ranklists.Any(y => y.Entries.Any(z => z.Participation.Combination.Horse.Id == id)))
            .ToList();
        if (recordsWithHorse.Any())
        {
            return new OkObjectResult(
                $"The horse you want to delete has participated in '{recordsWithHorse.Count}' events. It will not be removed from those archives, but will no longer be visible for future events"
            );
        }

        await _horses.Delete(id);

        return new OkObjectResult($"Deleted horse with id '{id}'");
    }

    [Function("horses-delete")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "horses/{id:int}")] HttpRequest request,
        int id
    )
    {
        LogInformation(request);

        await _horses.Delete(id);

        return new OkObjectResult($"Deleted horse with id '{id}'");
    }

    [Function("horses-get")]
    public async Task<IActionResult> GetOne(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "horses/{id:int}")] HttpRequest request,
        int id
    )
    {
        LogInformation(request);

        var horse = await _horses.Read(id);

        return new OkObjectResult(horse);
    }

    [Function("horses-list")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "horses")] HttpRequest request
    )
    {
        LogInformation(request);
        var horses = await _horses.ReadAll().Select(x => x.MaptoDomain());
        return new OkObjectResult(horses);
    }
}

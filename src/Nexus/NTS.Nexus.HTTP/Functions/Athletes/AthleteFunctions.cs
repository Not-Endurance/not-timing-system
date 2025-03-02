using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Not.Application.CRUD.Ports;
using Not.Concurrency.Extensions;
using Not.Serialization;
using NTS.Domain.Aggregates;
using NTS.Domain.Objects;
using NTS.Domain.Setup.Aggregates;
using NTS.Nexus.HTTP.Functions.Archive;
using NTS.Nexus.HTTP.Logger;
using NTS.Storage.Documents.Athletes;

namespace NTS.Nexus.HTTP.Functions.Athletes;

public class AthletesFunctions : FunctionBase<AthletesFunctions>
{
    readonly IRepository<AthleteDocument> _athletes;
    readonly IArchiveRepository _archive;

    public AthletesFunctions(
        IFunctionLogger<AthletesFunctions> logger,
        IRepository<AthleteDocument> athletes,
        IArchiveRepository archive
    )
        : base(logger)
    {
        _athletes = athletes;
        _archive = archive;
    }

    [Function("athlete-insert")]
    public async Task<IActionResult> Insert(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "athletes")] HttpRequest request
    )
    {
        LogInformation(request);

        var requestBody = await new StreamReader(request.Body).ReadToEndAsync();
        var athlete = requestBody.FromConvertedJson<Athlete>();
        var document = new AthleteDocument(athlete);
        await _athletes.Create(document);

        return new OkObjectResult($"Inserted {athlete}");
    }

    [Function("athlete-update")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "athletes")] HttpRequest request
    )
    {
        LogInformation(request);

        var requestBody = await new StreamReader(request.Body).ReadToEndAsync();
        var athlete = requestBody.FromConvertedJson<Athlete>();
        var document = new AthleteDocument(athlete);
        await _athletes.Update(document);

        return new OkObjectResult($"Updated {athlete}");
    }

    [Function("athlete-safe-delete")]
    public async Task<IActionResult> SafeDelete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "athletes/{id:int}/safe")]
            HttpRequest request,
        int id
    )
    {
        LogInformation(request);

        var recordsWithAthlete = await _archive
            .ReadAll(x =>
                x.Rankings.Any(y =>
                    y.Entries.Any(z => z.Participation.Combination.Athlete.Id == id)
                )
            )
            .ToList();

        if (recordsWithAthlete.Any())
        {
            return new OkObjectResult(
                $"The athlete you want to delete has participated in '{recordsWithAthlete.Count}' events. It will not be removed from those archives, but will no longer be visible for future events"
            );
        }

        await _athletes.Delete(id);
        return new OkObjectResult($"Deleted athlete with id '{id}'");
    }

    [Function("athlete-delete")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "athletes/{id:int}")]
            HttpRequest request,
        int id
    )
    {
        LogInformation(request);
        await _athletes.Delete(id);
        return new OkObjectResult($"Deleted athlete with id '{id}'");
    }

    [Function("athlete-get-one")]
    public async Task<IActionResult> GetOne(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "athletes/{id:int}")]
            HttpRequest request,
        int id
    )
    {
        LogInformation(request);
        var athlete = await _athletes.Read(id);
        return new OkObjectResult(athlete);
    }

    [Function("athlete-list")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "athletes")] HttpRequest request
    )
    {
        LogInformation(request);

        // TODO: Implement response mapping layer for documents back to aggregates
        var athletes = await _athletes
            .ReadAll()
            .Select(x => new Athlete(
                x.Id, 
                new Person(x.Names),
                x.FeiId,
                x.Country == null ? null : new Country(x.Country.IsoCode, x.Country.NfCode, x.Country.Name),
                x.Club == null ? null : new Club(x.Club.Name),
                x.Category));
        return new OkObjectResult(athletes);
    }
}

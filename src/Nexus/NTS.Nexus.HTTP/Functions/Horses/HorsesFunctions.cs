using DnsClient.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Not.Application.CRUD.Ports;
using Not.Concurrency.Extensions;
using Not.Serialization;
using NTS.Domain.Setup.Aggregates;
using NTS.Nexus.HTTP.Functions.Archive;
using NTS.Storage.Documents.Horses;

namespace NTS.Nexus.HTTP.Functions.Horses;

public  class HorsesFunctions
{
    readonly ILogger<HorsesFunctions> _logger;
    readonly IRepository<HorseDocument> _horses;
    readonly IArchiveRepository _archive;

    public HorsesFunctions(ILogger<HorsesFunctions> logger, IRepository<HorseDocument> horses, IArchiveRepository archive)
    {
        _logger = logger;
        _horses = horses;
        _archive = archive;
    }

    [Function("horse-insert")]
    public async Task<IActionResult> Insert([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "horses")] HttpRequest request)
    {
        _logger.LogInformation("C# HTTP '{function}' processing a request.", $"{nameof(HorsesFunctions)}.{nameof(Insert)}");

        var requestBody = await new StreamReader(request.Body).ReadToEndAsync();
        var horse = requestBody.FromJson<Horse>();
        var document = new HorseDocument(horse);
        await _horses.Create(document);

        return new OkObjectResult($"Inserted {horse}");
    }

    [Function("horse-update")]
    public async Task<IActionResult> Update([HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "horses")] HttpRequest request)
    {
        _logger.LogInformation("C# HTTP '{function}' processing a request.", $"{nameof(HorsesFunctions)}.{nameof(Insert)}");

        var requestBody = await new StreamReader(request.Body).ReadToEndAsync();
        var horse = requestBody.FromJson<Horse>();
        var document = new HorseDocument(horse);
        await _horses.Update(document);

        return new OkObjectResult($"Updated {horse}");
    }

    [Function("horse-safe-delete")]
    public async Task<IActionResult> SafeDelete([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "horses/{id:int}/safe")] HttpRequest request, int id)
    {
        _logger.LogInformation("C# HTTP '{function}' processing a request.", $"{nameof(HorsesFunctions)}.{nameof(SafeDelete)}");

        var requestBody = await new StreamReader(request.Body).ReadToEndAsync();

        var recordsWithHorse = await _archive
            .ReadAll(x => x.Rankings.Any(y => y.Entries.Any(z => z.Participation.Combination.Horse.Id == id)))
            .ToList();
        if (recordsWithHorse.Any())
        {
            return new OkObjectResult($"The horse you want to delete has participated in '{recordsWithHorse.Count}' events. It will not be removed from those archives, but will no longer be visible for future events");
        }

        await _horses.Delete(id);

        return new OkObjectResult($"Deleted horse with id '{id}'");
    }

    [Function("horse-delete")]
    public async Task<IActionResult> Delete([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "horses/{id:int}")] HttpRequest request, int id)
    {
        _logger.LogInformation("C# HTTP '{function}' processing a request.", $"{nameof(HorsesFunctions)}.{nameof(Delete)}");

        var requestBody = await new StreamReader(request.Body).ReadToEndAsync();

        await _horses.Delete(id);

        return new OkObjectResult($"Deleted horse with id '{id}'");
    }

    [Function("horse-get-one")]
    public async Task<IActionResult> GetOne([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "horses/{id:int}")] HttpRequest request, int id)
    {
        _logger.LogInformation("C# HTTP '{function}' processing a {request}.", $"{nameof(HorsesFunctions)}.{nameof(GetOne)}", request);

        var horse = await _horses.Read(id);

        return new OkObjectResult(horse);
    }

    [Function("horse-list")]
    public async Task<IActionResult> List([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "horses")] HttpRequest request)
    {
        _logger.LogInformation("C# HTTP '{function}' processing a {request}.", $"{nameof(HorsesFunctions)}.{nameof(List)}", request);

        var horses = await _horses.ReadAll();

        return new OkObjectResult(horses);
    }
}

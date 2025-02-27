using System.Security.Authentication;
using DnsClient.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Not.Application.CRUD.Ports;
using Not.Serialization;
using NTS.Domain.Setup.Aggregates;
using NTS.Storage.Documents.Horses;

namespace NTS.Nexus.HTTP.Functions.Horses;

public  class HorsesFunctions
{
    readonly ILogger<HorsesFunctions> _logger;
    readonly IRepository<HorseDocument> _horses;

    public HorsesFunctions(ILogger<HorsesFunctions> logger, IRepository<HorseDocument> horses)
    {
        _logger = logger;
        _horses = horses;
    }

    [Function("horse-insert")]
    public async Task<IActionResult> Insert([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "horses")] HttpRequest request)
    {
        _logger.LogInformation("C# HTTP 'ArchiveFunctions.Insert' processing a request.");

        var requestBody = await new StreamReader(request.Body).ReadToEndAsync();
        var horse = requestBody.FromJson<Horse>();
        var document = new HorseDocument(horse);
        await _horses.Create(document);

        return new OkObjectResult($"Inserted {horse}");
    }

    [Function("horse-update")]
    public async Task<IActionResult> Update([HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "horses")] HttpRequest request)
    {
        _logger.LogInformation("C# HTTP 'ArchiveFunctions.Insert' processing a request.");

        var requestBody = await new StreamReader(request.Body).ReadToEndAsync();
        var horse = requestBody.FromJson<Horse>();
        var document = new HorseDocument(horse);
        await _horses.Update(document);

        return new OkObjectResult($"Updated {horse}");
    }
}

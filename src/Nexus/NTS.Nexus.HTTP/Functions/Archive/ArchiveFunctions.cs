using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Not.Serialization;
using NTS.Domain.Core.Aggregates;
using NTS.Nexus.HTTP.Logger;
using NTS.Storage.Documents.EnduranceEvents;

namespace NTS.Nexus.HTTP.Functions.Archive;

public class ArchiveFunctions : FunctionBase<ArchiveFunctions>
{
    readonly IArchiveRepository _archive;

    public ArchiveFunctions(IArchiveRepository archive, IFunctionLogger<ArchiveFunctions> logger)
        : base(logger)
    {
        _archive = archive;
    }

    [Function("archive-insert")]
    public async Task<IActionResult> Insert(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "archive")] HttpRequest request
    )
    {
        LogInformation(request);

        var requestBody = await new StreamReader(request.Body).ReadToEndAsync();
        var entry = requestBody.FromConvertedJson<ArchiveEntry>();
        var document = new EnduranceEventDocument(
            entry.EnduranceEvent,
            entry.Officials,
            entry.Rankings
        );
        await _archive.Create(document);

        return new OkObjectResult($"Archived event {entry.EnduranceEvent}");
    }

    [Function("archive-list")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "archive")] HttpRequest request
    )
    {
        LogInformation(request);

        var result = await _archive.ReadAll();

        return new OkObjectResult(result);
    }

    [Function("archive-query-by-horse")]
    public async Task<IActionResult> QueryByHorse(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "archive/horse/{horseId:int}")]
            HttpRequest request,
        int horseId
    )
    {
        LogInformation(request);

        var performances = await _archive.GetPerformances(horseId);

        return new OkObjectResult(performances);
    }
}

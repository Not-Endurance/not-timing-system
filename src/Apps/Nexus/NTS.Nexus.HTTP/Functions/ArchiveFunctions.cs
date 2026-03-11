using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using NTS.Application.Core;
using NTS.Nexus.HTTP.Functions.Base;
using NTS.Nexus.HTTP.Logger;
using NTS.Nexus.HTTP.Mongo.Repositories;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Functions;

public class ArchiveFunctions : FunctionBase
{
    readonly IArchiveRepository _archive;

    public ArchiveFunctions(
        IArchiveRepository archive,
        IFunctionLogger<ArchiveFunctions> logger,
        ITelemetryService telemetry
    )
        : base(logger, telemetry)
    {
        _archive = archive;
    }

    [Function("archive-insert")]
    public async Task<IActionResult> Insert(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "archive")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(Insert));
        TagRequest(request);
        LogInformation(request, nameof(Insert));

        var document = await ReadBody<ArchiveEntryModel>(request);
        if (document == null)
        {
            return UnexpectedPayload<ArchiveEntryModel>();
        }

        var existing = await _archive.Read(document.Id);
        if (existing != null) // TODO: investigate this not working
        {
            await _archive.Delete(existing);
        }

        await _archive.Create(document);
        return new OkObjectResult($"Archived event with id '{document.Id}'");
    }

    [Function("archive-list")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "archive")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(List));
        TagRequest(request);
        LogInformation(request, nameof(List));

        var result = await _archive.ReadMany();
        return new OkObjectResult(result);
    }

    [Function("archive-query-by-horse")]
    public async Task<IActionResult> QueryByHorse(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "archive/horse/{horseId:int}")] HttpRequest request,
        int horseId
    )
    {
        using var activity = StartFunctionActivity(nameof(QueryByHorse));
        TagRequest(request);
        LogInformation(request, nameof(QueryByHorse));

        var archives = await _archive.GetPerformances(horseId);
        return new OkObjectResult(archives);
    }
}

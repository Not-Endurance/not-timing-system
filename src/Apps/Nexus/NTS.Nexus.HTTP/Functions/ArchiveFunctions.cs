using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using NTS.Application.Core;
using NTS.Domain.Core.Aggregates;
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
    public Task<IActionResult> Insert(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "archive")] HttpRequest request
    )
    {
        return ExecuteHttp(request, nameof(Insert), async () =>
        {
            var entry = await ReadBody<ArchiveEntry>(request);
            if (entry == null)
            {
                return UnexpectedPayload<ArchiveEntry>();
            }
            var document = ArchiveEntryModel.MapFrom(entry.EnduranceEvent, entry.Officials, entry.Ranklists);

            var existing = await ExecuteWithTelemetry("RepositoryReadById", () => _archive.Read(entry.Id));
            if (existing != null) // TODO: investigate this not working
            {
                await ExecuteWithTelemetry("RepositoryDelete", () => _archive.Delete(document));
            }

            await ExecuteWithTelemetry("RepositoryCreate", () => _archive.Create(document));
            return new OkObjectResult($"Archived event {entry.EnduranceEvent}");
        });
    }

    [Function("archive-list")]
    public Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "archive")] HttpRequest request
    )
    {
        return ExecuteHttp(request, nameof(List), async () =>
        {
            var result = await ExecuteWithTelemetry("RepositoryReadMany", () => _archive.ReadMany());
            return new OkObjectResult(result);
        });
    }

    [Function("archive-query-by-horse")]
    public Task<IActionResult> QueryByHorse(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "archive/horse/{horseId:int}")] HttpRequest request,
        int horseId
    )
    {
        return ExecuteHttp(request, nameof(QueryByHorse), async () =>
        {
            var archives = await ExecuteWithTelemetry("RepositoryGetPerformances", () => _archive.GetPerformances(horseId));
            return new OkObjectResult(archives);
        });
    }
}

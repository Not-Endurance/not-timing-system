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
    public Task<IActionResult> Insert(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "clubs")] HttpRequest request
    )
    {
        return ExecuteHttp(request, nameof(Insert), async () =>
        {
            var club = await ReadBody<Club>(request);
            if (club == null)
            {
                return UnexpectedPayload<Club>();
            }

            var document = ClubModel.MapFrom(club);
            await ExecuteWithTelemetry("RepositoryCreate", () => _clubs.Create(document));
            return new OkObjectResult($"Inserted {club}");
        });
    }

    [Function("clubs-update")]
    public Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "clubs")] HttpRequest request
    )
    {
        return ExecuteHttp(request, nameof(Update), async () =>
        {
            var club = await ReadBody<Club>(request);
            if (club == null)
            {
                return UnexpectedPayload<Club>();
            }

            var document = ClubModel.MapFrom(club);
            await ExecuteWithTelemetry("RepositoryUpdate", () => _clubs.Update(document));
            return new OkObjectResult($"Updated {club}");
        });
    }

    [Function("clubs-delete")]
    public Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "clubs/{id:int}")] HttpRequest request,
        int id
    )
    {
        return ExecuteHttp(request, nameof(Delete), async () =>
        {
            var club = await ExecuteWithTelemetry("RepositoryRead", () => _clubs.Read(id));
            if (club == null)
            {
                return new OkObjectResult($"Club wiht id '{id}' did not exist");
            }

            await ExecuteWithTelemetry("RepositoryDelete", () => _clubs.Delete(club));
            return new OkObjectResult($"Deleted club with id '{id}'");
        });
    }

    [Function("clubs-get-one")]
    public Task<IActionResult> GetOne(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "clubs/{id:int}")] HttpRequest request,
        int id
    )
    {
        return ExecuteHttp(request, nameof(GetOne), async () =>
        {
            var club = await ExecuteWithTelemetry("RepositoryRead", () => _clubs.Read(id));
            return new OkObjectResult(club?.MapToDomain());
        });
    }

    [Function("clubs-list")]
    public Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "clubs")] HttpRequest request
    )
    {
        return ExecuteHttp(request, nameof(List), async () =>
        {
            var clubs = await ExecuteWithTelemetry("RepositoryReadMany", () => _clubs.ReadMany().Select(x => x.MapToDomain()));
            return new OkObjectResult(clubs);
        });
    }
}

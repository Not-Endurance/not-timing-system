using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Not.Application.CRUD.Ports;
using NTS.Application.Setup;
using NTS.Nexus.HTTP.Functions.Base;
using NTS.Nexus.HTTP.Logger;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Functions;

public class AthleteFunctions : CrudFunctions<AthleteModel>
{
    public AthleteFunctions(
        IFunctionLogger<AthleteFunctions> logger,
        IRepository<AthleteModel> athletes,
        ITelemetryService telemetry
    )
        : base(logger, athletes, telemetry) { }

    [Function("athletes-create")]
    public Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "athletes")] HttpRequest request
    )
    {
        return ExecuteHttp(request, nameof(Create), () => InternalCreate(request));
    }

    [Function("athletes-update")]
    public Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "athletes")] HttpRequest request
    )
    {
        return ExecuteHttp(request, nameof(Update), () => InternalUpdate(request));
    }

    [Function("athletes-delete")]
    public Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "athletes/{id:int}")] HttpRequest request,
        int id
    )
    {
        return ExecuteHttp(request, nameof(Delete), () => InternalDelete(request, id));
    }

    [Function("athletes-read")]
    public Task<IActionResult> Read(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "athletes/{id:int}")] HttpRequest request,
        int id
    )
    {
        return ExecuteHttp(request, nameof(Read), () => InternalRead(request, id));
    }

    [Function("athletes-read-many")]
    public Task<IActionResult> ReadMany(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "athletes")] HttpRequest request
    )
    {
        return ExecuteHttp(request, nameof(ReadMany), () => InternalReadMany(request));
    }
}

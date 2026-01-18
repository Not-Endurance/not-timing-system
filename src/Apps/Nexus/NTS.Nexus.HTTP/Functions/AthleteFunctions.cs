using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Not.Application.CRUD.Ports;
using Not.Concurrency.Extensions;
using Not.Serialization.JSON;
using NTS.Application.Setup;
using NTS.Domain.Setup.Aggregates;
using NTS.Nexus.HTTP.Functions.Base;
using NTS.Nexus.HTTP.Logger;

namespace NTS.Nexus.HTTP.Functions;

public class AthleteFunctions : FunctionBase<AthleteFunctions>
{
    readonly IRepository<AthleteModel> _athletes;

    public AthleteFunctions(IFunctionLogger<AthleteFunctions> logger, IRepository<AthleteModel> athletes)
        : base(logger)
    {
        _athletes = athletes;
    }

    [Function("athletes-insert")]
    public async Task<IActionResult> Insert(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "athletes")] HttpRequest request
    )
    {
        LogInformation(request);

        var requestBody = await new StreamReader(request.Body).ReadToEndAsync();
        var athlete = requestBody.FromJson<Athlete>();
        var document = AthleteModel.MapFrom(athlete);
        await _athletes.Create(document);

        return new OkObjectResult($"Inserted {athlete}");
    }

    [Function("athletes-update")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "athletes")] HttpRequest request
    )
    {
        LogInformation(request);

        var requestBody = await new StreamReader(request.Body).ReadToEndAsync();
        var athlete = requestBody.FromJson<Athlete>();
        var document = AthleteModel.MapFrom(athlete);
        await _athletes.Update(document);

        return new OkObjectResult($"Updated {athlete}");
    }

    [Function("athletes-delete")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "athletes/{id:int}")] HttpRequest request,
        int id
    )
    {
        LogInformation(request);
        var athlete = await _athletes.Read(id);
        if (athlete == null)
        {
            return new OkObjectResult($"Club wiht id '{id}' did not exist");
        }
        await _athletes.Delete(athlete);
        return new OkObjectResult($"Deleted athlete with id '{id}'");
    }

    [Function("athletes-get-one")]
    public async Task<IActionResult> Get(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "athletes/{id:int}")] HttpRequest request,
        int id
    )
    {
        LogInformation(request);
        var athlete = await _athletes.Read(id);
        return new OkObjectResult(athlete?.MapToDomain());
    }

    [Function("athletes-list")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "athletes")] HttpRequest request
    )
    {
        LogInformation(request);

        // TODO: Implement response mapping layer for documents back to aggregates
        var athletes = await _athletes.ReadMany().Select(x => x.MapToDomain());
        return new OkObjectResult(athletes);
    }
}

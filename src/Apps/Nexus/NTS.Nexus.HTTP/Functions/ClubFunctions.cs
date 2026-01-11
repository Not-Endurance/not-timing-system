using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Not.Application.CRUD.Ports;
using Not.Async;
using Not.Serialization.JSON;
using NTS.Application.Models;
using NTS.Domain.Setup.Aggregates;
using NTS.Nexus.HTTP.Functions.Base;
using NTS.Nexus.HTTP.Logger;

namespace NTS.Nexus.HTTP.Functions;

public class ClubFunctions : FunctionBase<ClubFunctions>
{
    readonly IRepository<ClubModel> _clubs;

    public ClubFunctions(IFunctionLogger<ClubFunctions> logger, IRepository<ClubModel> clubs)
        : base(logger)
    {
        _clubs = clubs;
    }

    [Function("clubs-insert")]
    public async Task<IActionResult> Insert(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "clubs")] HttpRequest request
    )
    {
        LogInformation(request);

        var requestBody = await new StreamReader(request.Body).ReadToEndAsync();
        var club = requestBody.FromJson<Club>();
        var document = ClubModel.MapFrom(club);
        await _clubs.Create(document);

        return new OkObjectResult($"Inserted {club}");
    }

    [Function("clubs-update")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "clubs")] HttpRequest request
    )
    {
        LogInformation(request);

        var requestBody = await new StreamReader(request.Body).ReadToEndAsync();
        var club = requestBody.FromJson<Club>();
        var document = ClubModel.MapFrom(club);
        await _clubs.Update(document);

        return new OkObjectResult($"Updated {club}");
    }

    [Function("clubs-delete")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "clubs/{id:int}")] HttpRequest request,
        int id
    )
    {
        LogInformation(request);
        var club = await _clubs.Read(id);
        if (club == null)
        {
            return new OkObjectResult($"Club wiht id '{id}' did not exist");
        }
        await _clubs.Delete(club);
        return new OkObjectResult($"Deleted club with id '{id}'");
    }

    [Function("clubs-get-one")]
    public async Task<IActionResult> GetOne(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "clubs/{id:int}")] HttpRequest request,
        int id
    )
    {
        LogInformation(request);

        var club = await _clubs.Read(id);
        return new OkObjectResult(club?.MapToDomain());
    }

    [Function("clubs-list")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "clubs")] HttpRequest request
    )
    {
        LogInformation(request);

        var clubs = await _clubs.ReadMany().Select(x => x.MapToDomain());
        return new OkObjectResult(clubs);
    }
}

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Not.Application.CRUD.Ports;
using Not.Async;
using Not.Serialization;
using NTS.Domain.Aggregates;
using NTS.Nexus.HTTP.Logger;
using NTS.Storage.Documents.Clubs;

namespace NTS.Nexus.HTTP.Functions.Clubs;

public class ClubFunctions : FunctionBase<ClubFunctions>
{
    readonly IRepository<ClubDocument> _clubs;

    public ClubFunctions(IFunctionLogger<ClubFunctions> logger, IRepository<ClubDocument> clubs)
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
        var document = new ClubDocument(club);
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
        var document = new ClubDocument(club);
        await _clubs.Update(document);

        return new OkObjectResult($"Updated {club}");
    }

    [Function("clubs-delete")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "clubs/{id:int}")]
            HttpRequest request,
        int id
    )
    {
        LogInformation(request);

        await _clubs.Delete(id);
        return new OkObjectResult($"Deleted club with id '{id}'");
    }

    [Function("clubs-get-one")]
    public async Task<IActionResult> GetOne(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "clubs/{id:int}")]
            HttpRequest request,
        int id
    )
    {
        LogInformation(request);

        var club = await _clubs.Read(id);
        return new OkObjectResult(club?.ToDomain());
    }

    [Function("clubs-list")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "clubs")] HttpRequest request
    )
    {
        LogInformation(request);

        var clubs = await _clubs
            .ReadAll()
            .Select(x => x.ToDomain());
        return new OkObjectResult(clubs);
    }
}

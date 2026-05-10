using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Not.Storage.Mongo;
using NTS.Application.Contracts.Watcher;
using NTS.Application.Contracts.Watcher.Models;
using NTS.Application.UserSession;
using NTS.Nexus.HTTP.Functions.Base;
using NTS.Nexus.HTTP.Logger;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Functions;

public class UserSessionFunctions : CrudFunctions<NtsUserSessionModel>
{
    readonly INtsUserSessionRepository _sessions;

    public UserSessionFunctions(
        IFunctionLogger<UserSessionFunctions> logger,
        INtsUserSessionRepository sessions,
        IMongoRepository<NtsUserSessionModel> mongoSessions,
        ITelemetryService telemetry
    )
        : base(logger, mongoSessions, telemetry)
    {
        _sessions = sessions;
    }

    [Function("user-sessions-create")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "user-sessions")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(Create));
        TagRequest(request);
        LogInformation(request, nameof(Create));
        return await CreateCore(request);
    }

    [Function("user-sessions-update")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "user-sessions")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(Update));
        TagRequest(request);
        LogInformation(request, nameof(Update));
        return await UpdateCore(request);
    }

    [Function("user-sessions-delete")]
    public async Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "user-sessions/{id:int}")] HttpRequest request,
        int id
    )
    {
        using var activity = StartFunctionActivity(nameof(Delete));
        TagRequest(request);
        LogInformation(request, nameof(Delete));
        return await DeleteCore(id);
    }

    [Function("user-sessions-delete-for-event")]
    public async Task<IActionResult> DeleteForEvent(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "user-sessions/{eventId:int}/{id:int}")]
            HttpRequest request,
        int eventId,
        int id
    )
    {
        using var activity = StartFunctionActivity(nameof(DeleteForEvent));
        TagRequest(request);
        LogInformation(request, nameof(DeleteForEvent));

        await _sessions.Delete(new NtsUserSessionModel { Id = id, EventId = eventId });
        return Ok();
    }

    [Function("user-sessions-delete-many")]
    public async Task<IActionResult> DeleteMany(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "user-sessions")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(DeleteMany));
        TagRequest(request);
        LogInformation(request, nameof(DeleteMany));
        return await DeleteManyCore(request);
    }

    [Function("user-sessions-read")]
    public async Task<IActionResult> Read(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "user-sessions/{id:int}")] HttpRequest request,
        int id
    )
    {
        using var activity = StartFunctionActivity(nameof(Read));
        TagRequest(request);
        LogInformation(request, nameof(Read));
        return await ReadCore(id);
    }

    [Function("user-sessions-read-by-user-identifier")]
    public async Task<IActionResult> ReadByUserIdentifier(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "user-sessions/by-user-identifier/{userIdentifier}")]
            HttpRequest request,
        string userIdentifier
    )
    {
        using var activity = StartFunctionActivity(nameof(ReadByUserIdentifier));
        TagRequest(request);
        LogInformation(request, nameof(ReadByUserIdentifier));

        return Ok(await _sessions.ReadByUserIdentifier(Uri.UnescapeDataString(userIdentifier)));
    }

    [Function("user-sessions-read-by-user-identifier-and-event")]
    public async Task<IActionResult> ReadByUserIdentifierAndEvent(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "user-sessions/{eventId:int}/by-user-identifier/{userIdentifier}"
        )]
            HttpRequest request,
        int eventId,
        string userIdentifier
    )
    {
        using var activity = StartFunctionActivity(nameof(ReadByUserIdentifierAndEvent));
        TagRequest(request);
        LogInformation(request, nameof(ReadByUserIdentifierAndEvent));

        return Ok(await _sessions.ReadByUserIdentifier(Uri.UnescapeDataString(userIdentifier), eventId));
    }

    [Function("user-sessions-read-many")]
    public async Task<IActionResult> ReadMany(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "user-sessions")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(ReadMany));
        TagRequest(request);
        LogInformation(request, nameof(ReadMany));
        return await ReadManyCore(request);
    }
}

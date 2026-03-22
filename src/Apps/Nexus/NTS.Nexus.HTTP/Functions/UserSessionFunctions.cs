using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using NTS.Application.UserSession;
using NTS.Application.Watcher;
using NTS.Nexus.HTTP.Functions.Base;
using NTS.Nexus.HTTP.Logger;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Functions;

public class UserSessionFunctions : CrudFunctions<UserSessionModel>
{
    readonly IUserSessionRepository _sessions;

    public UserSessionFunctions(
        IFunctionLogger<UserSessionFunctions> logger,
        IUserSessionRepository sessions,
        ITelemetryService telemetry
    )
        : base(logger, sessions, telemetry)
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
        return await InternalCreate(request);
    }

    [Function("user-sessions-update")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "user-sessions")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(Update));
        TagRequest(request);
        LogInformation(request, nameof(Update));
        return await InternalUpdate(request);
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
        return await InternalDelete(request, id);
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
        return await InternalRead(request, id);
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

        var session = await _sessions.ReadByUserIdentifier(Uri.UnescapeDataString(userIdentifier));
        if (session == null)
        {
            return new NotFoundResult();
        }

        return Ok(session);
    }

    [Function("user-sessions-read-many")]
    public async Task<IActionResult> ReadMany(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "user-sessions")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(ReadMany));
        TagRequest(request);
        LogInformation(request, nameof(ReadMany));
        return await InternalReadMany(request);
    }
}

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Not.Storage.Mongo;
using NTS.Application.Contracts.Watcher;
using NTS.Application.Contracts.Watcher.Models;
using NTS.Application.UserSession;
using NoTiming.Api.Base;
using NoTiming.Api.Application.Logging;
using NoTiming.Api.Application.Telemetry;

namespace NoTiming.Api.Controllers;

public class UserSessionController : CrudControllerBase<NtsUserSessionModel>
{
    readonly INtsUserSessionRepository _sessions;

    public UserSessionController(
        IFunctionLogger<UserSessionController> logger,
        INtsUserSessionRepository sessions,
        IMongoRepository<NtsUserSessionModel> mongoSessions,
        ITelemetryService telemetry
    )
        : base(logger, mongoSessions, telemetry)
    {
        _sessions = sessions;
    }

    [HttpPost("user-sessions")]
    public async Task<IActionResult> Create()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Create));
        TagRequest(request);
        LogInformation(request, nameof(Create));
        return await CreateCore(request);
    }

    [HttpPatch("user-sessions")]
    public async Task<IActionResult> Update()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Update));
        TagRequest(request);
        LogInformation(request, nameof(Update));
        return await UpdateCore(request);
    }

    [HttpDelete("user-sessions/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Delete));
        TagRequest(request);
        LogInformation(request, nameof(Delete));
        return await DeleteCore(id);
    }

    [HttpDelete("user-sessions/{eventId:int}/{id:int}")]
    public async Task<IActionResult> DeleteForEvent(int eventId, int id)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(DeleteForEvent));
        TagRequest(request);
        LogInformation(request, nameof(DeleteForEvent));

        await _sessions.Delete(new NtsUserSessionModel { Id = id, EventId = eventId });
        return Ok();
    }

    [HttpDelete("user-sessions")]
    public async Task<IActionResult> DeleteMany()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(DeleteMany));
        TagRequest(request);
        LogInformation(request, nameof(DeleteMany));
        return await DeleteManyCore(request);
    }

    [HttpGet("user-sessions/{id:int}")]
    public async Task<IActionResult> Read(int id)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Read));
        TagRequest(request);
        LogInformation(request, nameof(Read));
        return await ReadCore(id);
    }

    [HttpGet("user-sessions/by-user-identifier/{userIdentifier}")]
    public async Task<IActionResult> ReadByUserIdentifier(string userIdentifier)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(ReadByUserIdentifier));
        TagRequest(request);
        LogInformation(request, nameof(ReadByUserIdentifier));

        return Ok(await _sessions.ReadByUserIdentifier(Uri.UnescapeDataString(userIdentifier)));
    }

    [HttpGet("user-sessions/{eventId:int}/by-user-identifier/{userIdentifier}")]
    public async Task<IActionResult> ReadByUserIdentifierAndEvent(int eventId, string userIdentifier)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(ReadByUserIdentifierAndEvent));
        TagRequest(request);
        LogInformation(request, nameof(ReadByUserIdentifierAndEvent));

        return Ok(await _sessions.ReadByUserIdentifier(Uri.UnescapeDataString(userIdentifier), eventId));
    }

    [HttpGet("user-sessions")]
    public async Task<IActionResult> ReadMany()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(ReadMany));
        TagRequest(request);
        LogInformation(request, nameof(ReadMany));
        return await ReadManyCore(request);
    }
}

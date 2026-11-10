using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using NC.Server.Authentication.Interface;
using NC.Storage.Mongo;
using NoTiming.Api.Application.Logging;
using NoTiming.Api.Application.Telemetry;
using NoTiming.Api.Base;
using NoTiming.Application.Contracts.Watcher.Models;
using NoTiming.Application.UserSession;

namespace NoTiming.Api.Controllers;

/// <summary>
/// A person's own event session. Every route needs a session, and a route that names or changes another person's
/// session is refused, so the Ui reads and writes only what the person who is signed in owns.
/// </summary>
public class UserSessionController : CrudControllerBase<NoTimingUserSessionModel>
{
    readonly INoTimingUserSessionRepository _sessions;
    readonly IAuthentication _authentication;

    public UserSessionController(
        IFunctionLogger<UserSessionController> logger,
        INoTimingUserSessionRepository sessions,
        IMongoRepository<NoTimingUserSessionModel> mongoSessions,
        IAuthentication authentication,
        ITelemetryService telemetry
    )
        : base(logger, mongoSessions, telemetry)
    {
        _sessions = sessions;
        _authentication = authentication;
    }

    [HttpPost("user-sessions")]
    public async Task<IActionResult> Create()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Create));
        TagRequest(request);
        LogInformation(request, nameof(Create));

        var userId = await _authentication.SignedInUserId(HttpContext, HttpContext.RequestAborted);
        if (userId is null)
        {
            return Unauthorized();
        }

        request.EnableBuffering();
        var payload = await ReadBody<NoTimingUserSessionModel>(request);
        if (!IsOwnedBy(payload.UserIdentifier, userId.Value))
        {
            return Forbidden();
        }

        request.Body.Position = 0;
        return await CreateCore(request);
    }

    [HttpPatch("user-sessions")]
    public async Task<IActionResult> Update()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Update));
        TagRequest(request);
        LogInformation(request, nameof(Update));

        var userId = await _authentication.SignedInUserId(HttpContext, HttpContext.RequestAborted);
        if (userId is null)
        {
            return Unauthorized();
        }

        request.EnableBuffering();
        var payload = await ReadBody<NoTimingUserSessionModel>(request);
        var stored = await Repository.Read(payload.Id);
        if (stored is not null && !IsOwnedBy(stored.UserIdentifier, userId.Value))
        {
            return Forbidden();
        }

        if (!IsOwnedBy(payload.UserIdentifier, userId.Value))
        {
            return Forbidden();
        }

        request.Body.Position = 0;
        return await UpdateCore(request);
    }

    [HttpDelete("user-sessions/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Delete));
        TagRequest(request);
        LogInformation(request, nameof(Delete));

        var userId = await _authentication.SignedInUserId(HttpContext, HttpContext.RequestAborted);
        if (userId is null)
        {
            return Unauthorized();
        }

        var stored = await Repository.Read(id);
        if (stored is not null && !IsOwnedBy(stored.UserIdentifier, userId.Value))
        {
            return Forbidden();
        }

        return await DeleteCore(id);
    }

    [HttpDelete("user-sessions/{eventId:int}/{id:int}")]
    public async Task<IActionResult> DeleteForEvent(int eventId, int id)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(DeleteForEvent));
        TagRequest(request);
        LogInformation(request, nameof(DeleteForEvent));

        var userId = await _authentication.SignedInUserId(HttpContext, HttpContext.RequestAborted);
        if (userId is null)
        {
            return Unauthorized();
        }

        var stored = await Repository.Read(id);
        if (stored is not null && !IsOwnedBy(stored.UserIdentifier, userId.Value))
        {
            return Forbidden();
        }

        await _sessions.Delete(new NoTimingUserSessionModel { Id = id, EventId = eventId });
        return Ok();
    }

    [HttpGet("user-sessions/{id:int}")]
    public async Task<IActionResult> Read(int id)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Read));
        TagRequest(request);
        LogInformation(request, nameof(Read));

        var userId = await _authentication.SignedInUserId(HttpContext, HttpContext.RequestAborted);
        if (userId is null)
        {
            return Unauthorized();
        }

        var stored = await Repository.Read(id);
        if (stored is not null && !IsOwnedBy(stored.UserIdentifier, userId.Value))
        {
            return Forbidden();
        }

        return Ok(stored);
    }

    [HttpGet("user-sessions/by-user-identifier/{userIdentifier}")]
    public async Task<IActionResult> ReadByUserIdentifier(string userIdentifier)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(ReadByUserIdentifier));
        TagRequest(request);
        LogInformation(request, nameof(ReadByUserIdentifier));

        var userId = await _authentication.SignedInUserId(HttpContext, HttpContext.RequestAborted);
        if (userId is null)
        {
            return Unauthorized();
        }

        var identifier = Uri.UnescapeDataString(userIdentifier);
        if (!IsOwnedBy(identifier, userId.Value))
        {
            return Forbidden();
        }

        return Ok(await _sessions.ReadByUserIdentifier(identifier));
    }

    [HttpGet("user-sessions/{eventId:int}/by-user-identifier/{userIdentifier}")]
    public async Task<IActionResult> ReadByUserIdentifierAndEvent(int eventId, string userIdentifier)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(ReadByUserIdentifierAndEvent));
        TagRequest(request);
        LogInformation(request, nameof(ReadByUserIdentifierAndEvent));

        var userId = await _authentication.SignedInUserId(HttpContext, HttpContext.RequestAborted);
        if (userId is null)
        {
            return Unauthorized();
        }

        var identifier = Uri.UnescapeDataString(userIdentifier);
        if (!IsOwnedBy(identifier, userId.Value))
        {
            return Forbidden();
        }

        return Ok(await _sessions.ReadByUserIdentifier(identifier, eventId));
    }

    static bool IsOwnedBy(string? userIdentifier, int userId)
    {
        return string.Equals(userIdentifier, userId.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    IActionResult Forbidden()
    {
        return StatusCode(StatusCodes.Status403Forbidden);
    }
}

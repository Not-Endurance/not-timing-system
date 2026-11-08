using Microsoft.AspNetCore.Mvc;
using NC.Server.Authentication.Sessions;
using NoTiming.Api.Application.Logging;
using NoTiming.Api.Application.Mongo.Repositories;
using NoTiming.Api.Application.Telemetry;
using NoTiming.Api.Base;

namespace NoTiming.Api.Controllers;

/// <summary>
/// The directory of users, which Judge searches. It answers only a signed-in person: an anonymous caller cannot query
/// it. A person's own profile is read and changed through the session routes, not here.
/// </summary>
public class UserController : ApiControllerBase
{
    readonly IUserRepository _users;

    public UserController(IFunctionLogger<UserController> logger, IUserRepository users, ITelemetryService telemetry)
        : base(logger, telemetry)
    {
        _users = users;
    }

    [HttpGet("users")]
    public async Task<IActionResult> ReadMany()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(ReadMany));
        TagRequest(request);
        LogInformation(request, nameof(ReadMany));

        if (await SessionUser.IdOfAsync(HttpContext) is null)
        {
            return Unauthorized();
        }

        return Ok(await _users.ReadMany() ?? []);
    }
}

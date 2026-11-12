using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NC.Server.Authentication.Interface;
using NoTiming.Api.Application.Account;
using NoTiming.Api.Application.Logging;
using NoTiming.Api.Application.Telemetry;
using NoTiming.Api.Base;
using NoTiming.Application.Contracts.Account;

namespace NoTiming.Api.Controllers;

/// <summary>
/// The directory of users, which Judge searches. It answers only a signed-in person: an anonymous caller cannot query
/// it. A person's own profile is read and changed through the session routes, not here.
/// </summary>
[Authorize(Policy = AuthenticationPolicies.SIGNED_IN)]
public class UserController : ApiControllerBase
{
    readonly IUserDirectory<NoTimingProfile> _users;

    public UserController(
        IFunctionLogger<UserController> logger,
        IUserDirectory<NoTimingProfile> users,
        ITelemetryService telemetry
    )
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

        var users = await _users.ReadAll(HttpContext.RequestAborted);
        return Ok(users.Select(UserModels.From).ToArray());
    }
}

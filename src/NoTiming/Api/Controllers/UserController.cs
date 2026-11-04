using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Not.Application.Authentication.User;
using NoTiming.Api.Base;
using NoTiming.Api.Logging;
using NoTiming.Api.Mongo.Repositories;
using NoTiming.Api.Telemetry;
using NoTiming.Ui.Contracts.API;

namespace NoTiming.Api.Controllers;

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

        return Ok(await _users.ReadMany() ?? []);
    }

    [HttpGet("users/{email}")]
    public async Task<IActionResult> ReadByEmail(string email)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(ReadByEmail));
        TagRequest(request);
        LogInformation(request, nameof(ReadByEmail));

        return Ok(await _users.ReadByEmail(email));
    }

    [HttpPost("users/register")]
    public async Task<IActionResult> Register()
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(Register));
        TagRequest(request);
        LogInformation(request, nameof(Register));

        var payload = await ReadBody<RegisterUserPaload>(request);
        if (string.IsNullOrWhiteSpace(payload.Email))
        {
            return InvalidPayload($"Value '{payload.Email}' is not a valid email");
        }

        var user = await _users.Register(
            new NUserRegistration(
                payload.Email,
                payload.Name,
                payload.GivenName,
                payload.Surname,
                payload.CountryRegion,
                payload.MiddleName,
                payload.Club,
                payload.FeiId,
                payload.DisplayName
            )
        );

        return Ok(user);
    }

    [HttpPatch("users/{email}/profile")]
    public async Task<IActionResult> UpdateProfile(string email)
    {
        var request = Request;
        using var activity = StartFunctionActivity(nameof(UpdateProfile));
        TagRequest(request);
        LogInformation(request, nameof(UpdateProfile));

        if (string.IsNullOrWhiteSpace(email))
        {
            return InvalidPayload($"Value '{email}' is not a valid email");
        }

        var payload = await ReadBody<UpdateUserProfilePayload>(request);
        if (!payload.HasRequiredProfile())
        {
            return InvalidPayload("Country, first name and last name are required");
        }

        var user = await _users.UpdateProfile(email, payload);
        return user == null ? Failure($"User '{email}' was not found") : Ok(user);
    }
}

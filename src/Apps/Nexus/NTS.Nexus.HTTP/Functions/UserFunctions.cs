using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Not.Application.Authentication.User;
using NTS.Nexus.HTTP.Functions.Base;
using NTS.Nexus.HTTP.Logger;
using NTS.Nexus.HTTP.Mongo.Repositories;
using NTS.Nexus.HTTP.Telemetry;
using NTS.Witness.Contracts.API;

namespace NTS.Nexus.HTTP.Functions;

public class UserFunctions : FunctionBase
{
    readonly IUserRepository _users;

    public UserFunctions(IFunctionLogger<UserFunctions> logger, IUserRepository users, ITelemetryService telemetry)
        : base(logger, telemetry)
    {
        _users = users;
    }

    [Function("users-read-many")]
    public async Task<IActionResult> ReadMany(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "users")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(ReadMany));
        TagRequest(request);
        LogInformation(request, nameof(ReadMany));

        return Ok(await _users.ReadMany() ?? []);
    }

    [Function("users-read-by-email")]
    public async Task<IActionResult> ReadByEmail(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "users/{email}")] HttpRequest request,
        string email
    )
    {
        using var activity = StartFunctionActivity(nameof(ReadByEmail));
        TagRequest(request);
        LogInformation(request, nameof(ReadByEmail));

        return Ok(await _users.ReadByEmail(email));
    }

    [Function("users-register")]
    public async Task<IActionResult> Register(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "users/register")] HttpRequest request
    )
    {
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

    [Function("users-update-profile")]
    public async Task<IActionResult> UpdateProfile(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "users/{email}/profile")] HttpRequest request,
        string email
    )
    {
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

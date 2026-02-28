using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using NTS.Nexus.HTTP.Functions.Base;
using NTS.Nexus.HTTP.Logger;
using NTS.Nexus.HTTP.Mongo.Repositories;
using NTS.Witness.Contracts.API;

namespace NTS.Nexus.HTTP.Functions;

public class UserFunctions : FunctionBase
{
    readonly IUserRepository _users;

    public UserFunctions(IFunctionLogger<UserFunctions> logger, IUserRepository users)
        : base(logger)
    {
        _users = users;
    }

    [Function("users-read-by-email")]
    public async Task<IActionResult> ReadByEmail(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "users/{email}")]
            HttpRequest request,
        string email
    )
    {
        LogInformation(request);

        var user = await _users.ReadByEmail(email);
        if (user == null)
        {
            return NotFound(email);
        }

        return Ok(user);
    }

    [Function("users-register")]
    public async Task<IActionResult> Register(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "users/register")] HttpRequest request
    )
    {
        LogInformation(request);

        var payload = await ReadBody<RegisterUserPaload>(request);
        if (string.IsNullOrWhiteSpace(payload?.Email))
        {
            return InvalidPayload($"Value '{payload?.Email}' is not a valid email");
        }

        var user = await _users.Register(payload.Email);
        return Ok(user);
    }
}

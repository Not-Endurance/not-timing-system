using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Not.Application.CRUD.Ports;
using NTS.Application.Setup;
using NTS.Nexus.HTTP.Functions.Base;
using NTS.Nexus.HTTP.Logger;

namespace NTS.Nexus.HTTP.Functions;

public class UserFunctions : CrudFunctions<UserModel>
{
    public UserFunctions(IFunctionLogger<UserFunctions> logger, IRepository<UserModel> users)
        : base(logger, users) { }

    [Function("users-create")]
    public Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "users")] HttpRequest request
    )
    {
        return InternalCreate(request);
    }

    [Function("users-update")]
    public Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "users")] HttpRequest request
    )
    {
        return InternalUpdate(request);
    }

    [Function("users-delete")]
    public Task<IActionResult> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "users/{id:int}")] HttpRequest request,
        int id
    )
    {
        return InternalDelete(request, id);
    }

    [Function("users-read")]
    public Task<IActionResult> Read(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "users/{id:int}")] HttpRequest request,
        int id
    )
    {
        return InternalRead(request, id);
    }

    [Function("users-read-many")]
    public Task<IActionResult> ReadMany(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "users")] HttpRequest request
    )
    {
        return InternalReadMany(request);
    }
}

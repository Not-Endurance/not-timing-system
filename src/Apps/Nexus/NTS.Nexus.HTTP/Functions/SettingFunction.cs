using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Not.Application.CRUD.Ports;
using NTS.Application.Shared;
using NTS.Domain.Aggregates;
using NTS.Nexus.HTTP.Functions.Base;
using NTS.Nexus.HTTP.Logger;
using NTS.Nexus.HTTP.Telemetry;

namespace NTS.Nexus.HTTP.Functions;

public class SettingFunction : FunctionBase
{
    readonly IRepository<SettingModel> _settings;

    public SettingFunction(
        IFunctionLogger<SettingFunction> logger,
        IRepository<SettingModel> settings,
        ITelemetryService telemetry
    )
        : base(logger, telemetry)
    {
        _settings = settings;
    }

    [Function("settings-insert")]
    public Task<IActionResult> Insert(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "settings")] HttpRequest request
    )
    {
        return ExecuteHttp(request, nameof(Insert), async () =>
        {
            var setting = await ReadBody<Setting>(request);
            if (setting == null)
            {
                return UnexpectedPayload<Setting>();
            }

            var document = SettingModel.MapFrom(setting);
            await ExecuteWithTelemetry("RepositoryCreate", () => _settings.Create(document));
            return new OkObjectResult($"Inserted {setting}");
        });
    }

    [Function("settings-update")]
    public Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "settings")] HttpRequest request
    )
    {
        return ExecuteHttp(request, nameof(Update), async () =>
        {
            var setting = await ReadBody<Setting>(request);
            if (setting == null)
            {
                return UnexpectedPayload<Setting>();
            }

            var document = SettingModel.MapFrom(setting);
            await ExecuteWithTelemetry("RepositoryUpdate", () => _settings.Update(document));
            return new OkObjectResult($"Updated {setting}");
        });
    }

    [Function("settings-get")]
    public Task<IActionResult> GetOne(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "settings/{accountId}")] HttpRequest request,
        string accountId
    )
    {
        return ExecuteHttp(request, nameof(GetOne), async () =>
        {
            var setting = await ExecuteWithTelemetry("RepositoryReadByAccount", () => _settings.Read(x => x.AccountId == accountId));
            return new OkObjectResult(setting?.ToDomain());
        });
    }
}

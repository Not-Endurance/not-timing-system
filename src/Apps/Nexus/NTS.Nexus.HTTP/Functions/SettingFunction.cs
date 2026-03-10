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
    public async Task<IActionResult> Insert(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "settings")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(Insert));
        TagRequest(request);
        LogInformation(request, nameof(Insert));

        var setting = await ReadBody<Setting>(request);
        if (setting == null)
        {
            return UnexpectedPayload<Setting>();
        }

        var document = SettingModel.From(setting);
        await _settings.Create(document);
        return new OkObjectResult($"Inserted {setting}");
    }

    [Function("settings-update")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "settings")] HttpRequest request
    )
    {
        using var activity = StartFunctionActivity(nameof(Update));
        TagRequest(request);
        LogInformation(request, nameof(Update));

        var setting = await ReadBody<Setting>(request);
        if (setting == null)
        {
            return UnexpectedPayload<Setting>();
        }

        var document = SettingModel.From(setting);
        await _settings.Update(document);
        return new OkObjectResult($"Updated {setting}");
    }

    [Function("settings-get")]
    public async Task<IActionResult> GetOne(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "settings/{accountId}")] HttpRequest request,
        string accountId
    )
    {
        using var activity = StartFunctionActivity(nameof(GetOne));
        TagRequest(request);
        LogInformation(request, nameof(GetOne));

        var setting = await _settings.Read(x => x.AccountId == accountId);
        return new OkObjectResult(setting?.MapToEntity());
    }
}

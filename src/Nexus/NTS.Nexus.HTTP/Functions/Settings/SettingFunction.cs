using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Not.Application.CRUD.Ports;
using Not.Serialization.JSON;
using NTS.Domain.Settings;
using NTS.Nexus.HTTP.Logger;

namespace NTS.Nexus.HTTP.Functions.Settings;

public class SettingFunction : FunctionBase<SettingFunction>
{
    readonly IRepository<SettingModel> _settings;

    public SettingFunction(IFunctionLogger<SettingFunction> logger, IRepository<SettingModel> settings)
        : base(logger)
    {
        _settings = settings;
    }

    [Function("settings-insert")]
    public async Task<IActionResult> Insert(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "settings")] HttpRequest request
    )
    {
        LogInformation(request);

        var requestBody = await new StreamReader(request.Body).ReadToEndAsync();
        var setting = requestBody.FromJson<Setting>();
        var document = SettingModel.MapFrom(setting);
        await _settings.Create(document);

        return new OkObjectResult($"Inserted {setting}");
    }

    [Function("settings-update")]
    public async Task<IActionResult> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "settings")] HttpRequest request
    )
    {
        LogInformation(request);

        var requestBody = await new StreamReader(request.Body).ReadToEndAsync();
        var setting = requestBody.FromJson<Setting>();
        var document = SettingModel.MapFrom(setting);
        await _settings.Update(document);

        return new OkObjectResult($"Updated {setting}");
    }

    [Function("settings-get")]
    public async Task<IActionResult> GetOne(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "settings/{accountId}")] HttpRequest request,
        string accountId
    )
    {
        LogInformation(request);

        var setting = await _settings.Read(x => x.AccountId == accountId);

        return new OkObjectResult(setting?.ToDomain());
    }
}

using NC.Application.HTTP;
using NC.Storage.REST;
using NoTiming.Application.Contracts.Shared;
using NoTiming.Application.Contracts.Shared.Models;
using NoTiming.Application.Settings;
using NoTiming.Domain.Aggregates;

namespace NoTiming.Storage.REST;

public class SettingApiRepository : ApiRepository<Setting, SettingModel>, ISettingRepository
{
    public SettingApiRepository(NHttpClient client)
        : base("settings", client) { }

    public async Task<Setting?> Get(Guid accountId)
    {
        var url = BuildEndpoint(accountId);
        var model = await HandleRequest(Client.Get<SettingModel>(url));
        return MapEntity(model);
    }
}

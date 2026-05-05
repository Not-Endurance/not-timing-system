using Not.Application.HTTP;
using Not.Injection;
using Not.Storage.REST;
using NTS.Application.Contracts.Shared;
using NTS.Application.Contracts.Shared.Models;
using NTS.Application.Settings;
using NTS.Domain.Aggregates;

namespace NTS.Storage.REST;

public class SettingRestApiRepository : ApiRepository<Setting, SettingModel>, ISettingRepository, ITransient
{
    public SettingRestApiRepository(NHttpClient client)
        : base("settings", client) { }

    public async Task<Setting?> Get(Guid accountId)
    {
        var url = BuildEndpoint(accountId);
        var model = await HandleRequest(Client.Get<SettingModel>(url));
        return MapEntity(model);
    }
}

using Not.Application.HTTP;
using Not.Injection;
using Not.Storage.REST;
using NTS.Application.Shared;
using NTS.Domain.Aggregates;
using NTS.Judge.Features.Settings;

namespace NTS.Storage.REST;

public class SettingRestApiRepository : RestApiRepository<Setting, SettingModel>, ISettingRepository, ITransient
{
    public SettingRestApiRepository(NHttpClient client)
        : base("settings", client) { }

    public async Task<Setting?> Get(Guid accountId)
    {
        try
        {
            var url = BuildUrl(accountId);
            var model = await Client.GetJson<SettingModel>(url);
            return MapEntity(model);
        }
        catch (Exception ex)
        {
            HandleException(ex);
            return null;
        }
    }
}

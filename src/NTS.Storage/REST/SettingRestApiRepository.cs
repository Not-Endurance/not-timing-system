using Not.Application.HTTP;
using Not.Injection;
using Not.Storage.REST;
using NTS.Application.Shared;
using NTS.Domain.Aggregates;
using NTS.Judge.Features.Settings;

namespace NTS.Storage.REST;

public class SettingRestApiRepository : RestApiRepository2<Setting, SettingModel>, ISettingRepository, ITransient
{
    public SettingRestApiRepository(NHttpClient client)
        : base("settings", client) { }

    public async Task<Setting?> Get(Guid accountId)
    {
        try
        {
            var url = BuildUrl(accountId);
            return await Client.GetJson<Setting>(url);
        }
        catch (Exception ex)
        {
            HandleException(ex);
            return null;
        }
    }
}

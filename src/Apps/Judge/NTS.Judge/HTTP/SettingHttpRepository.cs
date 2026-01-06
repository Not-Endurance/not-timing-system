using Not.Application.CRUD.Ports;
using Not.Application.HTTP;
using Not.Storage.REST;
using NTS.Domain.Settings;

namespace NTS.Judge.HTTP;

public class SettingHttpRepository : RestApiRepository<Setting>, ISettingRepository
{
    public SettingHttpRepository(NHttpClient client)
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

public interface ISettingRepository : IRepository<Setting>
{
    Task<Setting?> Get(Guid accountId);
}

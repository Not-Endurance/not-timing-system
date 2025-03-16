using Not.Application.CRUD.Ports;
using Not.Application.HTTP;
using Not.Serialization.JSON;
using NTS.Domain.Settings;

namespace NTS.Judge.HTTP;

public class SettingHttpRepository : HttpRepository<Setting>, ISettingRepository
{
    public SettingHttpRepository(NHttpClient client)
        : base("settings", client) { }

    public async Task<Setting?> Get(Guid accountId)
    {
        try
        {
            var url = BuildUrl(accountId);
            var contents = await Client.Get(url);
            return contents?.FromJson<Setting>();
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

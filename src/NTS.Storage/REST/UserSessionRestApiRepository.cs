using Not.Application.HTTP;
using Not.Injection;
using Not.Storage.REST;
using NTS.Application.UserSession;
using NTS.Application.Watcher;

namespace NTS.Storage.REST;

public class UserSessionRestApiRepository
    : RestApiRepository<NtsUserSessionModel, NtsUserSessionModel>,
        INtsUserSessionRepository,
        ITransient
{
    public UserSessionRestApiRepository(NHttpClient client)
        : base("user-sessions", client) { }

    public async Task<NtsUserSessionModel?> ReadByUserIdentifier(string userIdentifier)
    {
        if (string.IsNullOrWhiteSpace(userIdentifier))
        {
            return null;
        }

        try
        {
            var encodedUserIdentifier = Uri.EscapeDataString(userIdentifier);
            return await Client.GetJson<NtsUserSessionModel>($"{Endpoint}/by-user-identifier/{encodedUserIdentifier}");
        }
        catch (Exception ex)
        {
            HandleException(ex);
            return null;
        }
    }
}

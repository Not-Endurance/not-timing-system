using Not.Application.HTTP;
using Not.Injection;
using Not.Storage.REST;
using NTS.Application.UserSession;
using NTS.Application.Watcher;

namespace NTS.Storage.REST;

public class UserSessionRestApiRepository
    : RestApiRepository<UserSessionModel, UserSessionModel>,
        IUserSessionRepository,
        ITransient
{
    public UserSessionRestApiRepository(NHttpClient client)
        : base("user-sessions", client) { }

    public async Task<UserSessionModel?> ReadByUserIdentifier(string userIdentifier)
    {
        if (string.IsNullOrWhiteSpace(userIdentifier))
        {
            return null;
        }

        try
        {
            var encodedUserIdentifier = Uri.EscapeDataString(userIdentifier);
            return await Client.GetJson<UserSessionModel>($"{Endpoint}/by-user-identifier/{encodedUserIdentifier}");
        }
        catch (Exception ex)
        {
            HandleException(ex);
            return null;
        }
    }
}

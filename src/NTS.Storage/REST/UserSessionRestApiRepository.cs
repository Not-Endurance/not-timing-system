using Not.Application.Authentication.Abstractions;
using Not.Application.HTTP;
using Not.Injection;
using Not.Storage.REST;
using NTS.Application.Contracts.Watcher;
using NTS.Application.Contracts.Watcher.Models;
using NTS.Application.UserSession;

namespace NTS.Storage.REST;

public class UserSessionRestApiRepository
    : RestApiRepository<NtsUserSessionModel, NtsUserSessionModel>,
        INtsUserSessionRepository,
        INUserSessionRepository<NtsUserSessionStateModel>,
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

        var encodedUserIdentifier = Uri.EscapeDataString(userIdentifier);
        return await HandleRequest(
            Client.Get<NtsUserSessionModel>($"{Endpoint}/by-user-identifier/{encodedUserIdentifier}")
        );
    }

    async Task<NtsUserSessionStateModel?> INUserSessionRepository<NtsUserSessionStateModel>.ReadByUserIdentifier(
        string userIdentifier
    )
    {
        return (await ReadByUserIdentifier(userIdentifier))?.State?.Copy();
    }
}

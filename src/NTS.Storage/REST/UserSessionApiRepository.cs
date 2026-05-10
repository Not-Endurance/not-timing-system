using Not.Application.Authentication.Abstractions;
using Not.Application.HTTP;
using Not.Storage.REST;
using NTS.Application.Contracts.Watcher;
using NTS.Application.Contracts.Watcher.Models;
using NTS.Application.UserSession;

namespace NTS.Storage.REST;

public class UserSessionApiRepository
    : ApiRepository<NtsUserSessionModel, NtsUserSessionModel>,
        INtsUserSessionRepository,
        INUserSessionRepository<NtsUserSessionStateModel>
{
    public UserSessionApiRepository(NHttpClient client)
        : base("user-sessions", client) { }

    public new async Task Delete(NtsUserSessionModel item)
    {
        await HandleRequest(Client.Delete($"{Endpoint}/{item.EventId}/{item.Id}"));
    }

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

    public async Task<NtsUserSessionModel?> ReadByUserIdentifier(string userIdentifier, int eventId)
    {
        if (string.IsNullOrWhiteSpace(userIdentifier))
        {
            return null;
        }

        var encodedUserIdentifier = Uri.EscapeDataString(userIdentifier);
        return await HandleRequest(
            Client.Get<NtsUserSessionModel>($"{Endpoint}/{eventId}/by-user-identifier/{encodedUserIdentifier}")
        );
    }

    async Task<NtsUserSessionStateModel?> INUserSessionRepository<NtsUserSessionStateModel>.ReadByUserIdentifier(
        string userIdentifier
    )
    {
        return (await ReadByUserIdentifier(userIdentifier))?.State?.Copy();
    }
}

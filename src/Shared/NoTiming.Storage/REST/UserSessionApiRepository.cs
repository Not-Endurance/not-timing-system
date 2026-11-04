using NC.Application.Authentication.Abstractions;
using NC.Application.HTTP;
using NC.Storage.REST;
using NoTiming.Application.Contracts.Watcher;
using NoTiming.Application.Contracts.Watcher.Models;
using NoTiming.Application.UserSession;

namespace NoTiming.Storage.REST;

public class UserSessionApiRepository
    : ApiRepository<NoTimingUserSessionModel, NoTimingUserSessionModel>,
        INoTimingUserSessionRepository,
        INUserSessionRepository<NoTimingUserSessionStateModel>
{
    public UserSessionApiRepository(NHttpClient client)
        : base("user-sessions", client) { }

    public new async Task Delete(NoTimingUserSessionModel item)
    {
        await HandleRequest(Client.Delete($"{Endpoint}/{item.EventId}/{item.Id}"));
    }

    public async Task<NoTimingUserSessionModel?> ReadByUserIdentifier(string userIdentifier)
    {
        if (string.IsNullOrWhiteSpace(userIdentifier))
        {
            return null;
        }

        var encodedUserIdentifier = Uri.EscapeDataString(userIdentifier);
        return await HandleRequest(
            Client.Get<NoTimingUserSessionModel>($"{Endpoint}/by-user-identifier/{encodedUserIdentifier}")
        );
    }

    public async Task<NoTimingUserSessionModel?> ReadByUserIdentifier(string userIdentifier, int eventId)
    {
        if (string.IsNullOrWhiteSpace(userIdentifier))
        {
            return null;
        }

        var encodedUserIdentifier = Uri.EscapeDataString(userIdentifier);
        return await HandleRequest(
            Client.Get<NoTimingUserSessionModel>($"{Endpoint}/{eventId}/by-user-identifier/{encodedUserIdentifier}")
        );
    }

    async Task<NoTimingUserSessionStateModel?> INUserSessionRepository<NoTimingUserSessionStateModel>.ReadByUserIdentifier(
        string userIdentifier
    )
    {
        return (await ReadByUserIdentifier(userIdentifier))?.State?.Copy();
    }
}

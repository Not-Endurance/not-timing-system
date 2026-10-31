using Not.Application.HTTP;
using Not.Blazor.Client.REST;
using NTS.Contracts.Sessions.Models;
using NTS.Domain.Core.Sessions;

namespace NoTiming.Ui.Storage.REST;

/// <summary>
/// What a person keeps per Event as the Api serves it (#602, #645): the Snapshots selected and the groups sent, at
/// <c>/api/user-sessions</c>. The Api finds the records of the account that is signed in, and of no other.
/// </summary>
public class UserSessionApiRepository : JsonApiRepository<UserSession, UserSessionModel>
{
    public UserSessionApiRepository(JsonApiClient client)
        : base("user-sessions", client) { }

    /// <summary>The record of the account for the Event, when it has one.</summary>
    public async Task<UserSessionModel?> FindOf(Guid eventId)
    {
        var models = await ModelsWithin(x => x.EventId == eventId);
        return models.FirstOrDefault();
    }

    /// <summary>
    /// The record of the account for the Event, made when there is none. The Api answers with the record that is there, so
    /// that a record of another tab is never replaced by a new one.
    /// </summary>
    public async Task<UserSessionModel> Make(Guid eventId)
    {
        var response = await Taken(Client.Post(Collection, new UserSessionModel { EventId = eventId }));
        return ModelOf(ResourceOf(response))
            ?? throw new InvalidOperationException("The Api kept a session that cannot be read.");
    }

    /// <summary>Puts the state of the record in place of the one it has.</summary>
    public async Task Save(UserSessionModel session)
    {
        await Taken(Client.Patch(Collection, session.Id, session));
    }
}

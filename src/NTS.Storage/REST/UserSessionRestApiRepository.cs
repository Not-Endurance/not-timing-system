using Not.Application.HTTP;
using Not.Injection;
using Not.Storage.REST;
using NTS.Application.Watcher;

namespace NTS.Storage.REST;

public class UserSessionRestApiRepository : RestApiRepository<UserSessionModel, UserSessionModel>, ITransient
{
    public UserSessionRestApiRepository(NHttpClient client)
        : base("user-sessions", client) { }
}

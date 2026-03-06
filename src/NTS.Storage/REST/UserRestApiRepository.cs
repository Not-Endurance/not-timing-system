using Not.Application.HTTP;
using Not.Injection;
using Not.Storage.REST;
using NTS.Application.Setup;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Storage.REST;

public class UserRestApiRepository : RestApiRepository2<User, UserModel>, ITransient
{
    public UserRestApiRepository(NHttpClient client)
        : base("users", client) { }
}

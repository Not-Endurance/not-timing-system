using Not.Application.HTTP;
using Not.Injection;
using Not.Storage.REST;
using NTS.Application.Setup;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Witness.Storage.Repositories;

public class UserRepository : RestApiRepository2<User, UserModel>, ITransient
{
    public UserRepository(NHttpClient client)
        : base("users", client) { }
}

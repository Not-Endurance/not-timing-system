using NC.Application.HTTP;
using NC.Storage.REST;
using NC.Strings;
using NoTiming.Application.Contracts.Setup;
using NoTiming.Application.Contracts.Setup.Models;
using NoTiming.Application.Setup;
using NoTiming.Domain.Setup.Aggregates;

namespace NoTiming.Storage.REST;

public class UserApiRepository : ApiRepository<User, UserModel>, IUserLookup
{
    public UserApiRepository(NHttpClient client)
        : base("users", client) { }

    public async Task<IEnumerable<User>> Search(string term)
    {
        var users = await ReadMany();
        if (string.IsNullOrWhiteSpace(term))
        {
            return users;
        }

        return users.Where(x => UserSearchPolicy.IsMatch(x, term));
    }
}

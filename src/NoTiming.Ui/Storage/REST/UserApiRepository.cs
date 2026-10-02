using Not.Application.Authentication.User;
using Not.Application.HTTP;
using Not.Storage.REST;
using Not.Strings;
using NTS.Application.Setup;
using NTS.Contracts.Setup;
using NTS.Contracts.Setup.Models;
using NTS.Domain.Setup.Aggregates;

namespace NoTiming.Ui.Storage.REST;

public class UserApiRepository : ApiRepository<User, UserModel>, IUserLookup
{
    public UserApiRepository(NHttpClient client)
        : base("users", client) { }

    public async Task<User?> ReadByEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var encodedEmail = Uri.EscapeDataString(email.Trim());
        var user = await HandleRequest(Client.Get<NUserModel>($"{Endpoint}/{encodedEmail}"));
        return user == null
            ? null
            : new User(
                user.Email,
                user.Name,
                user.Roles,
                user.Id,
                user.GivenName,
                user.MiddleName,
                user.Surname,
                user.CountryRegion,
                user.Club,
                user.FeiId,
                user.DisplayName
            );
    }

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

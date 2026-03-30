using Not.Application.Authentication.User;
using Not.Application.HTTP;
using Not.Injection;
using Not.Storage.REST;
using Not.Strings;
using NTS.Application.Setup;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Storage.REST;

public class UserRestApiRepository : RestApiRepository<User, UserModel>, IUserEmailLookup, ITransient
{
    public UserRestApiRepository(NHttpClient client)
        : base("users", client) { }

    public async Task<User?> ReadByEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        try
        {
            var encodedEmail = Uri.EscapeDataString(email.Trim());
            var user = await Client.GetJson<NUserModel>($"{Endpoint}/{encodedEmail}");
            return user == null ? null : new User(user.Email, user.Name, user.Roles, user.Id);
        }
        catch (Exception ex)
        {
            HandleException(ex);
            return null;
        }
    }

    public async Task<IEnumerable<User>> Search(string term)
    {
        var users = await ReadMany();
        if (string.IsNullOrWhiteSpace(term))
        {
            return users;
        }

        return users.Where(x => x.Name.NContains(term) || x.Email.NContains(term));
    }
}

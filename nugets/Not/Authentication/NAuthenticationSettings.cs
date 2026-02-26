using Not.Authentication.User;

namespace Not.Authentication;

public class NAuthenticationSettings : IAuthenticationSettings
{
    readonly IAuthenticationUserStore _users;

    public NAuthenticationSettings(IAuthenticationUserStore users)
    {
        _users = users;
    }

    public async Task<NUser?> ResolveUser(string email, string? name)
    {
        var user = await _users.ReadByEmail(email);
        if (user != null)
        {
            return user;
        }

        return await _users.Create(email, name);
    }
}

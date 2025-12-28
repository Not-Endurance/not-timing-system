using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Not.Authentication.User;

namespace Not.Authentication;

public class NAuthenticationSettings : IAuthenticationSettings
{
    public NAuthenticationSettings(IOptions<AuthOptions> authOptions)
    {
        Users = authOptions.Value.Users;
    }

    public List<NUser> Users { get; set; } = [];

    public NUser? GetUserByEmail(string email)
    {
        var user = Users.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
        return user;
    }
}

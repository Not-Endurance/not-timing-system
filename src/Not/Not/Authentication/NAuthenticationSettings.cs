using Microsoft.Extensions.Configuration;
using Not.Authentication.User;

namespace Not.Authentication;

public class NAuthenticationSettings : IUserDeserializer
{
    public List<NUser> Users { get; set; } = [];

    public List<NUser> GetAllowedUsers(IConfiguration configuration)
    {
        Users = configuration.GetSection("Auth:Users").Get<List<NUser>>() ?? [];
        return Users;
    }
}

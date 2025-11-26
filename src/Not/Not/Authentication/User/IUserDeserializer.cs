
using Microsoft.Extensions.Configuration;

namespace Not.Authentication.User;
public interface IUserDeserializer
{
    public Dictionary<string, NUser> GetAllowedUsers(IConfiguration configuration);
}

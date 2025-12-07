using Microsoft.Extensions.Configuration;

namespace Not.Authentication.User;

public interface IUserDeserializer
{
    public List<NUser> GetAllowedUsers(IConfiguration configuration);
}

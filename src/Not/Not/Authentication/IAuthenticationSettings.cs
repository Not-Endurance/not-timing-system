using Not.Authentication.User;

namespace Not.Authentication;

public interface IAuthenticationSettings
{
    public NUser? GetUserByEmail(string email);
}

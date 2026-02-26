using Not.Authentication.User;

namespace Not.Authentication;

public interface IAuthenticationSettings
{
    public Task<NUser?> ResolveUser(string email, string? name);
}

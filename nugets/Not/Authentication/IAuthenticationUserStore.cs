using Not.Authentication.User;

namespace Not.Authentication;

public interface IAuthenticationUserStore
{
    Task<NUser?> ReadByEmail(string email);
    Task<NUser?> Create(string email, string? name);
}

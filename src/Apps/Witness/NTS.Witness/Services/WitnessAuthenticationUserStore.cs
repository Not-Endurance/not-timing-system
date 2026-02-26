using Not.Application.CRUD.Ports;
using Not.Authentication;
using Not.Authentication.User;
using Not.Injection;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Witness.Services;

public class WitnessAuthenticationUserStore : IAuthenticationUserStore, IScoped
{
    readonly IRepository<User> _users;

    public WitnessAuthenticationUserStore(IRepository<User> users)
    {
        _users = users;
    }

    public async Task<NUser?> ReadByEmail(string email)
    {
        var user = await ReadUserByEmail(email);
        return user == null ? null : ToAuthUser(user);
    }

    public async Task<NUser?> Create(string email, string? name)
    {
        await _users.Create(new User(email, name, []));
        var user = await ReadUserByEmail(email);
        return user == null ? null : ToAuthUser(user);
    }

    async Task<User?> ReadUserByEmail(string email)
    {
        var users = await _users.ReadMany();
        return users.FirstOrDefault(x => x.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
    }

    static NUser ToAuthUser(User user)
    {
        return new NUser { Email = user.Email, Roles = user.Roles.ToArray() };
    }
}

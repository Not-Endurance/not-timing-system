using NTS.Domain.Setup.Aggregates;

namespace NTS.Application.Setup;

// TODO: Create IUserRepository, implement this one in a NTS service
public interface IUserEmailLookup
{
    Task<User?> ReadByEmail(string email);
    Task<IEnumerable<User>> Search(string term);
}

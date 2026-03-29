using NTS.Domain.Setup.Aggregates;

namespace NTS.Application.Setup;

public interface IUserEmailLookup
{
    Task<User?> ReadByEmail(string email);
    Task<IEnumerable<User>> Search(string term);
}

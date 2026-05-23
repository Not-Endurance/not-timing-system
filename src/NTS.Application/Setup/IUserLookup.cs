using NTS.Domain.Setup.Aggregates;

namespace NTS.Application.Setup;

public interface IUserLookup
{
    Task<User?> ReadByEmail(string email);
    Task<IEnumerable<User>> Search(string term);
}

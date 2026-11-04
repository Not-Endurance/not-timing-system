using NoTiming.Domain.Setup.Aggregates;

namespace NoTiming.Application.Setup;

public interface IUserLookup
{
    Task<User?> ReadByEmail(string email);
    Task<IEnumerable<User>> Search(string term);
}

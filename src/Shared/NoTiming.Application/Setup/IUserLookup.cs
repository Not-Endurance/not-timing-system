using NoTiming.Domain.Setup.Aggregates;

namespace NoTiming.Application.Setup;

public interface IUserLookup
{
    Task<IEnumerable<User>> Search(string term);
}

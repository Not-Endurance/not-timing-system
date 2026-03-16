using Not.Application.HTTP;
using Not.Injection;
using Not.Storage.REST;
using NTS.Application.Setup;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Storage.REST;

public class ClubRestApiRepository : RestApiRepository<Club, ClubModel>, ITransient
{
    public ClubRestApiRepository(NHttpClient client)
        : base("clubs", client) { }
}

using Not.Application.HTTP;
using Not.Storage.REST;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Storage.REST;

public class ClubRestApiRepository : RestApiRepository<Club>
{
    public ClubRestApiRepository(NHttpClient client)
        : base("clubs", client) { }
}

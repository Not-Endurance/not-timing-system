using Not.Application.HTTP;
using Not.Storage.REST;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Storage.REST;

public class AthleteRestApiRepository : RestApiRepository<Athlete>
{
    public AthleteRestApiRepository(NHttpClient client)
        : base("athletes", client) { }
}

using NC.Application.HTTP;
using NC.Storage.REST;
using NoTiming.Application.Contracts.Setup;
using NoTiming.Application.Contracts.Setup.Models;
using NoTiming.Domain.Setup.Aggregates;

namespace NoTiming.Storage.REST;

public class ClubApiRepository : ApiRepository<Club, ClubModel>
{
    public ClubApiRepository(NHttpClient client)
        : base("clubs", client) { }
}

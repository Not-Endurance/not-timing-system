using Not.Application.HTTP;
using Not.Storage.REST;
using NTS.Contracts.Setup;
using NTS.Contracts.Setup.Models;
using NTS.Domain.Setup.Aggregates;

namespace NoTiming.Ui.Storage.REST;

public class ClubApiRepository : ApiRepository<Club, ClubModel>
{
    public ClubApiRepository(NHttpClient client)
        : base("clubs", client) { }
}

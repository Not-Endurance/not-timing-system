using Not.Application.HTTP;
using Not.Injection;
using Not.Storage.REST;
using NTS.Application.Contracts.Setup;
using NTS.Application.Contracts.Setup.Models;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Storage.REST;

public class ClubRestApiRepository : ApiRepository<Club, ClubModel>, ITransient
{
    public ClubRestApiRepository(NHttpClient client)
        : base("clubs", client) { }
}

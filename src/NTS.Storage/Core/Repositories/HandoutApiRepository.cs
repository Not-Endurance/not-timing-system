using Not.Application.HTTP;
using Not.Storage.REST;
using NTS.Contracts.Core.Models;
using NTS.Domain.Core.Aggregates;

namespace NTS.Storage.Core.Repositories;

public class HandoutApiRepository : ApiRepository<Handout, HandoutModel>
{
    public HandoutApiRepository(NHttpClient client)
        : base("handouts", client) { }
}

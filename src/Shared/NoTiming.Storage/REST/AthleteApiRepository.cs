using NC.Application.CRUD.Ports;
using NC.Application.HTTP;
using NC.Storage.REST;
using NoTiming.Application.Contracts.Setup;
using NoTiming.Application.Contracts.Setup.Models;
using NoTiming.Domain.Setup.Aggregates;

namespace NoTiming.Storage.REST;

public class AthleteApiRepository : ApiRepository<Athlete, AthleteModel>
{
    public AthleteApiRepository(NHttpClient client)
        : base("athletes", client) { }
}

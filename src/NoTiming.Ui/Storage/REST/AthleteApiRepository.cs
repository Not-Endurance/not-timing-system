using Not.Application.CRUD.Ports;
using Not.Application.HTTP;
using Not.Storage.REST;
using NTS.Contracts.Setup;
using NTS.Contracts.Setup.Models;
using NTS.Domain.Setup.Aggregates;

namespace NoTiming.Ui.Storage.REST;

public class AthleteApiRepository : ApiRepository<Athlete, AthleteModel>
{
    public AthleteApiRepository(NHttpClient client)
        : base("athletes", client) { }
}

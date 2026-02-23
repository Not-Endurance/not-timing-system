using Not.Application.HTTP;
using Not.Injection;
using Not.Storage.REST;
using NTS.Application.Setup;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Storage.REST;

public class UpcomingEventRestApiRepository : RestApiRepository2<UpcomingEvent, UpcomingEventModel>, ITransient
{
    public UpcomingEventRestApiRepository(NHttpClient httpClient)
        : base("upcoming-event", httpClient) { }
}

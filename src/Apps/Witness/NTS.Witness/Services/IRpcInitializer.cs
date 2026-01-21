using Not.Injection;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Witness.Services;

public interface IRpcInitializer : ISingleton
{
    UpcomingEvent? ConnectedEvent { get; }
    bool IsConnected();
    Task StartConnection(UpcomingEvent enduranceEven);
    Task Disconnect();
}

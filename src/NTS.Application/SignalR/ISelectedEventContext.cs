using Not.Injection;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Application.SignalR;

public interface ISelectedEventContext : ISingleton
{
    public UpcomingEvent? Event { get; }
}

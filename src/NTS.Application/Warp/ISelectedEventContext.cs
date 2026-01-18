using Not.Injection;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Application.Warp;

public interface ISelectedEventContext : ISingleton
{
    public UpcomingEvent? Event { get; }
}

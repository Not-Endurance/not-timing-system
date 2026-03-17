using System;
using Not.Injection;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Features.Setup.UpcomingEvents;

public class SelectedUpcomingEventContext : ISelectedUpcomingEventContext, ISingleton
{
    public UpcomingEvent? Event { get; set; }
}

public interface ISelectedUpcomingEventContext
{
    UpcomingEvent? Event { get; set; }
}

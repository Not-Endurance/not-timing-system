using Not.Application.CRUD.Ports;
using Not.Injection;
using Not.Krud.Services;
using NTS.Contracts.Features.Console.Setup;
using NTS.Domain.Setup.Aggregates;

namespace NoTiming.Ui.Features.Console.Setup;

/// <summary>
/// The form of the Event itself: its name, its location and its FEI show ID (#646). An Event that has not started is
/// not removed here: the Tenant Root removes it, and nothing of the Setup pages offers to.
/// </summary>
public class SetupEventService : KrudServiceBase<ConfigureEvent, ConfigureEventFormModel>, ITransient
{
    public SetupEventService(IRepository<ConfigureEvent> setups)
        : base(setups, []) { }
}

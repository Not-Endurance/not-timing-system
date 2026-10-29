using Not.Application.CRUD.Ports;
using Not.Domain.Exceptions;
using Not.Injection;
using NTS.Contracts.Core.Models;

namespace NTS.Nexus.HTTP.Functions;

public interface IConfigureEventMutationGuard
{
    Task EnsureCanMutate(Guid configureEventId);
    Task EnsureCanMutate(IEnumerable<Guid> configureEventIds);
}

public class ConfigureEventMutationGuard : IConfigureEventMutationGuard, ITransient
{
    readonly IRepository<EventInformationModel> _eventInformation;

    public ConfigureEventMutationGuard(IRepository<EventInformationModel> eventInformation)
    {
        _eventInformation = eventInformation;
    }

    public async Task EnsureCanMutate(Guid configureEventId)
    {
        // A Setup is frozen from the day its Event starts, which is the day a Core document of the Event exists: the
        // Console works on the copies the Event made, and the Event is reset to be configured again (ADR-0012).
        var startedEvent = await _eventInformation.Read(x => x.Id == configureEventId);
        if (startedEvent == null)
        {
            return;
        }

        throw new DomainException($"Cannot mutate configure event '{configureEventId}' because the event is started.");
    }

    public async Task EnsureCanMutate(IEnumerable<Guid> configureEventIds)
    {
        foreach (var configureEventId in configureEventIds.Distinct())
        {
            await EnsureCanMutate(configureEventId);
        }
    }
}

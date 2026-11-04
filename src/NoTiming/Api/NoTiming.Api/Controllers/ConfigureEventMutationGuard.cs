using NC.Application.CRUD.Ports;
using NC.Domain.Exceptions;
using NC.Injection;
using NoTiming.Application.Contracts.Core.Models;

namespace NoTiming.Api.Controllers;

public interface IConfigureEventMutationGuard
{
    Task EnsureCanMutate(int configureEventId);
    Task EnsureCanMutate(IEnumerable<int> configureEventIds);
}

public class ConfigureEventMutationGuard : IConfigureEventMutationGuard, ITransient
{
    readonly IRepository<EventInformationModel> _eventInformation;

    public ConfigureEventMutationGuard(IRepository<EventInformationModel> eventInformation)
    {
        _eventInformation = eventInformation;
    }

    public async Task EnsureCanMutate(int configureEventId)
    {
        var activeEvent = await _eventInformation.Read(x => x.Id == configureEventId && x.IsActive);
        if (activeEvent == null)
        {
            return;
        }

        throw new DomainException($"Cannot mutate configure event '{configureEventId}' because the event is started.");
    }

    public async Task EnsureCanMutate(IEnumerable<int> configureEventIds)
    {
        foreach (var configureEventId in configureEventIds.Distinct())
        {
            await EnsureCanMutate(configureEventId);
        }
    }
}

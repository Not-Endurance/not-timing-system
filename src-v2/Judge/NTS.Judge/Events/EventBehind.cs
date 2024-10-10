using NTS.Domain.Setup.Entities;
using Not.Application.Ports.CRUD;
using Not.Blazor.Ports.Behinds;
using Not.Exceptions;
using Not.Safe;

namespace NTS.Judge.Events;

public class EventBehind : INotBehind<Event>, INotSetBehind<Official>, INotSetBehind<Competition>, INotParentBehind<Event>
{
    private readonly IRepository<Event> _eventRepository;
    private Event? _event;

    public EventBehind(IRepository<Event> eventRepository)
    {
        _eventRepository = eventRepository;
    }

    Task<IEnumerable<Official>> SafeGetAllOfficials()
    {
        return Task.FromResult(_event?.Officials.AsEnumerable() ?? []);
    }

    Task<IEnumerable<Competition>> SafeGetAllCompetitions()
    {
        return Task.FromResult(_event?.Competitions.AsEnumerable() ?? []);
    }

    async Task<Event?> SafeRead(int id)
    {
        _event = await _eventRepository.Read(id);
        return _event;
    }

    async Task<Event> SafeCreate(Event entity)
    {
        await _eventRepository.Create(entity);
        return _event = entity;
    }

    async Task<Event> SafeUpdate(Event entity)
    {
        return await _eventRepository.Update(entity);
    }

    Task<Event> SafeDelete(Event @event)
    {
        throw new NotImplementedException();
    }

    async Task<Official> SafeCreate(Official child)
    {
        GuardHelper.ThrowIfDefault(_event);

        _event.Add(child);
        await _eventRepository.Update(_event);
        return child;
    }

    async Task<Official> SafeDelete(Official child)
    {
        GuardHelper.ThrowIfDefault(_event);

        _event.Remove(child);
        await _eventRepository.Update(_event);
        return child;
    }

    async Task<Official> SafeUpdate(Official child)
    {
        GuardHelper.ThrowIfDefault(_event);

        _event.Update(child);
        await _eventRepository.Update(_event);
        return child;
    }

    async Task<Competition> SafeCreate(Competition child)
    {
        GuardHelper.ThrowIfDefault(_event);

        _event.Add(child);
        await _eventRepository.Update(_event);
        return child;
    }

    async Task<Competition> SafeDelete(Competition child)
    {
        GuardHelper.ThrowIfDefault(_event);

        _event.Remove(child);
        await _eventRepository.Update(_event);
        return child;
    }

    async Task<Competition> SafeUpdate(Competition child)
    {
        GuardHelper.ThrowIfDefault(_event);

        _event.Update(child);
        await _eventRepository.Update(_event);
        return child;
    }

    async Task<Event?> SafeInitialize(int id)
    {
        return _event = await _eventRepository.Read(id);
    }

    #region SafePattern 

    async Task<IEnumerable<Official>> IReadAllBehind<Official>.GetAll()
    {
        return await SafeHelper.Run(SafeGetAllOfficials) ?? [];
    }

    async Task<IEnumerable<Competition>> IReadAllBehind<Competition>.GetAll()
    {
        return await SafeHelper.Run(SafeGetAllCompetitions) ?? [];
    }

    public async Task<Event> Create(Event @event)
    {
        return await SafeHelper.Run(() => SafeCreate(@event)) ?? @event;
    }

    public async Task<Event?> Read(int id)
    {
        return await SafeHelper.Run(() => SafeRead(id));
    }

    public async Task<Event> Update(Event @event)
    {
        return await SafeHelper.Run(() => SafeUpdate(@event)) ?? @event;
    }

    public async Task<Event> Delete(Event @event)
    {
        return await SafeHelper.Run(() => SafeDelete(@event)) ?? @event;
    }

    public async Task<Official> Create(Official official)
    {
        return await SafeHelper.Run(() => SafeCreate(official)) ?? official;
    }

    public async Task<Official> Update(Official official)
    {
        return await SafeHelper.Run(() => SafeUpdate(official)) ?? official;
    }

    public async Task<Official> Delete(Official official)
    {
        return await SafeHelper.Run(() => SafeDelete(official)) ?? official;
    }

    public async Task<Competition> Create(Competition competition)
    {
        return await SafeHelper.Run(() => SafeCreate(competition)) ?? competition;
    }

    public async Task<Competition> Update(Competition competition)
    {
        return await SafeHelper.Run(() => SafeUpdate(competition)) ?? competition;
    }

    public async Task<Competition> Delete(Competition competition)
    {
        return await SafeHelper.Run(() => SafeDelete(competition)) ?? competition;
    }

    public async Task<Event?> Initialize(int id)
    {
        return await SafeHelper.Run(() => SafeInitialize(id));
    }

    #endregion
}

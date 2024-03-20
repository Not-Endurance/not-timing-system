﻿using NTS.Domain.Setup.Entities;
using Not.Application.Ports.CRUD;
using Not.Exceptions;

namespace NTS.Persistence.Adapters;

public class EventRepository : ParentRepository<Official, Competition, Event, State>, IParentRepository<Official>, IParentRepository<Competition>
{

    public EventRepository(IStore<State> store) : base(store)
    {
    }

    public override async Task<Event> Update(Event entity)
    {
        var context = await Store.Load();
        GuardHelper.ThrowIfNull(context.Event);

        foreach (var official in context.Event.Officials)
        {
            entity.Add(official);
        }
        foreach (var competition in context.Event.Competitions)
        {
            entity.Add(competition);
        }
        context.Event = entity;
        await Store.Commit(context);

        return entity;
    }

    public async Task<Official> Update(Official child)
    {
        var context = await Store.Load();
        var existing = context.Officials.Find(x => x == child);
        GuardHelper.ThrowIfNull(existing);

        context.Event.Update(child);
        await Store.Commit(context);

        return child;
    }
    public async Task<Competition> Update(Competition entity)
    {
        var context = await Store.Load();
        var existing = context.Competitions.FirstOrDefault();
        GuardHelper.ThrowIfNull(existing);

        context.Competitions.Remove(entity);
        context.Competitions.Add(entity);
        await Store.Commit(context);

        return entity;
    }
}
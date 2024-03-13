﻿using NTS.Domain.Objects;
using NTS.Domain.Setup.Entities;

namespace NTS.Judge.Blazor.Setup.Events;

public class EventFormModel
{
    public EventFormModel()
    {
    }
    public EventFormModel(Event @event)
    {
        this.Id = @event.Id;
        this.Place = @event.Place;
        this.Country = @event.Country;
    }

    public int? Id { get; }
    public string? Place { get; set; }
    public Country? Country { get; set; }
}
﻿using JsonNet.PrivatePropertySetterResolver;
using Newtonsoft.Json;
using NTS.Domain.Core.Objects;

namespace NTS.Domain.Core.Entities;

public class Event : DomainEntity
{
    private Event(int id) : base(id)
    {
    }
    public Event(
        Country country,
        string city,
        string place,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        string? feiId,
        string? feiCode, 
        string? showFeiId)
    {
        PopulatedPlace = new PopulatedPlace(country, city, place);
        EventSpan = new EventSpan(startDate, endDate);
        FeiId = feiId;
        FeiCode = feiCode;
        ShowFeiId = showFeiId;
    }

    public PopulatedPlace PopulatedPlace { get; private set; }
    public EventSpan EventSpan { get; private set; }
    public string? FeiId { get; private set; }
    public string? FeiCode { get; private set; }
    public string? ShowFeiId { get; private set; }

    public override string ToString()
    {
        return $"{PopulatedPlace} {EventSpan}";
    }
}

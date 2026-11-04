using NC.Krud.Abstractions;
using NoTiming.Application.Contracts.Shared;
using NoTiming.Application.Contracts.Shared.Models;
using NoTiming.Domain.Enums;
using NoTiming.Domain.Setup.Aggregates;
using NoTiming.Domain.Setup.Aggregates.ConfigureEvents;

namespace NoTiming.Application.Contracts.Setup.Models;

public class LoopModel
{
    public static LoopModel MapFrom(Loop loop)
    {
        return new() { Id = loop.Id, Distance = loop.Distance };
    }

    public int Id { get; init; }
    public double Distance { get; init; }

    public Loop MapToEntity()
    {
        return new Loop(Distance, Id);
    }
}

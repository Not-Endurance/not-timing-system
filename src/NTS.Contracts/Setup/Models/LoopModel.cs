using Not.Krud.Abstractions;
using NTS.Contracts.Shared;
using NTS.Contracts.Shared.Models;
using NTS.Domain.Enums;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;

namespace NTS.Contracts.Setup.Models;

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

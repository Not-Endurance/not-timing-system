using Newtonsoft.Json;
using Not.Krud.Abstractions;
using NTS.Application.Contracts.Shared;
using NTS.Application.Contracts.Shared.Models;
using NTS.Domain.Enums;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;

namespace NTS.Application.Contracts.Setup.Models;

public class PhaseModel
{
    public static PhaseModel Create(Phase phase)
    {
        return new PhaseModel
        {
            Id = phase.Id,
            Loop = LoopModel.MapFrom(phase.Loop!),
            Recovery = phase.Recovery,
            Rest = phase.Rest,
            IsCompulsoryInspectionRequired = phase.IsCompulsoryInspectionRequired,
        };
    }

    public int Id { get; set; }
    public int Recovery { get; init; }
    public LoopModel Loop { get; init; } = default!;
    public int? Rest { get; init; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool IsCompulsoryInspectionRequired { get; init; }

    public Phase MapToEntity()
    {
        var loop = Loop.MapToEntity();
        return new Phase(loop, Recovery, Rest, Id, IsCompulsoryInspectionRequired);
    }
}

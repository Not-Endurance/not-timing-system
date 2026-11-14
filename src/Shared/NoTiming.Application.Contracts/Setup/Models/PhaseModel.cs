using NC.Krud.Abstractions;
using Newtonsoft.Json;
using NoTiming.Application.Contracts.Shared;
using NoTiming.Application.Contracts.Shared.Models;
using NoTiming.Domain.Enums;
using NoTiming.Domain.Setup.Aggregates;
using NoTiming.Domain.Setup.Aggregates.ConfigureEvents;

namespace NoTiming.Application.Contracts.Setup.Models;

public class PhaseModel
{
    public static PhaseModel Create(Phase phase)
    {
        return new PhaseModel
        {
            DomainVersion = phase.DomainVersion,
            Id = phase.Id,
            Loop = LoopModel.MapFrom(phase.Loop!),
            Recovery = phase.Recovery,
            Rest = phase.Rest,
            IsCompulsoryInspectionRequired = phase.IsCompulsoryInspectionRequired,
        };
    }

    public int DomainVersion { get; set; }

    public int Id { get; set; }
    public int Recovery { get; init; }
    public LoopModel Loop { get; init; } = default!;
    public int? Rest { get; init; }

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool IsCompulsoryInspectionRequired { get; init; }

    public Phase MapToEntity()
    {
        var loop = Loop.MapToEntity();
        return new Phase(loop, Recovery, Rest, Id, IsCompulsoryInspectionRequired, storedDomainVersion: DomainVersion);
    }
}

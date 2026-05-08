using Not.Krud.Abstractions;
using NTS.Application.Contracts.Shared;
using NTS.Application.Contracts.Shared.Models;
using NTS.Domain.Enums;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;

namespace NTS.Application.Contracts.Setup.Models;

public class ParticipationModel
{
    public static ParticipationModel MapFrom(Participation participation)
    {
        return new ParticipationModel
        {
            Id = participation.Id,
            IsNotRanked = participation.IsNotRanked,
            Category = participation.Category,
            Combination = CombinationModel.MapFrom(participation.Combination),
            StartTimeOverride = participation.StartTimeOverride,
            MaxSpeedOverride = participation.MaxSpeedOverride,
            MinSpeedOverride = participation.MinSpeedOverride,
            MinAverageSpeed = participation.MinAverageSpeed,
            MaxAverageSpeed = participation.MaxAverageSpeed,
        };
    }

    public int Id { get; init; }
    public ParticipationCategory Category { get; init; } = default!;
    public CombinationModel Combination { get; init; } = default!;
    public bool IsNotRanked { get; init; }
    public DateTimeOffset? StartTimeOverride { get; init; }
    public double? MaxSpeedOverride { get; init; }
    public double? MinSpeedOverride { get; init; }
    public double? MinAverageSpeed { get; init; }
    public double? MaxAverageSpeed { get; init; }

    public Participation MapToEntity()
    {
        var combination = Combination.MapToEntity();
        return new Participation(
            IsNotRanked,
            combination,
            Category,
            StartTimeOverride,
            MaxSpeedOverride,
            MinSpeedOverride,
            MinAverageSpeed,
            MaxAverageSpeed,
            Id
        );
    }
}

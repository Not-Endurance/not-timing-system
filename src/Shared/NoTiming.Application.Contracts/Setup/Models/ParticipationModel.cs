using NC.Krud.Abstractions;
using NoTiming.Application.Contracts.Shared;
using NoTiming.Application.Contracts.Shared.Models;
using NoTiming.Domain.Enums;
using NoTiming.Domain.Setup.Aggregates;
using NoTiming.Domain.Setup.Aggregates.ConfigureEvents;

namespace NoTiming.Application.Contracts.Setup.Models;

public class ParticipationModel
{
    public static ParticipationModel MapFrom(Participation participation)
    {
        return new ParticipationModel
        {
            DomainVersion = participation.DomainVersion,
            Id = participation.Id,
            IsNotRanked = participation.IsNotRanked,
            Category = participation.Category,
            Combination = CombinationModel.MapFrom(participation.Combination),
            StartTimeOverride = participation.StartTimeOverride,
            Restrictions = SpeedRestrictionsModel.From(participation.Restrictions),
        };
    }

    public int DomainVersion { get; set; }

    public int Id { get; init; }
    public ParticipationCategory Category { get; init; } = default!;
    public CombinationModel Combination { get; init; } = default!;
    public bool IsNotRanked { get; init; }
    public DateTimeOffset? StartTimeOverride { get; init; }
    public SpeedRestrictionsModel Restrictions { get; init; } = new();

    public Participation MapToEntity()
    {
        var combination = Combination.MapToEntity();
        return new Participation(
            IsNotRanked,
            combination,
            Category,
            StartTimeOverride,
            Restrictions.ToDomain(),
            Id,
            storedDomainVersion: DomainVersion
        );
    }
}

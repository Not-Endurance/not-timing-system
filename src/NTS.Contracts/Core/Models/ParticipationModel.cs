using Not.Krud.Abstractions;
using NTS.Contracts.Shared;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Enums;

namespace NTS.Contracts.Core.Models;

public class ParticipationModel
    : IDocument,
        IEventScoped,
        ISoftDeletableDocument,
        IVersionedDocument,
        IKrudModel<Participation>
{
    public static ParticipationModel MapFrom(Participation participation)
    {
        return new ParticipationModel
        {
            Id = participation.Id,
            EventId = participation.EventId,
            Category = participation.Category,
            Competition = CompetitionModel.MapFrom(participation.Competition),
            Combination = CombinationModel.MapFrom(participation.Combination),
            Phases = participation.Phases.Select(PhaseModel.MapFrom).ToArray(),
            Eliminated = participation.Eliminated == null ? null : EliminatedModel.MapFrom(participation.Eliminated),
            Version = participation.Version,
        };
    }

    public Guid Id { get; set; }
    public string TenantId { get; set; } = StorageConstants.DEFAULT_TENANT;
    public Guid EventId { get; set; }
    public ParticipationCategory Category { get; set; } = default!;
    public CompetitionModel Competition { get; set; } = default!;
    public CombinationModel Combination { get; set; } = default!;
    public PhaseModel[] Phases { get; set; } = default!;
    public EliminatedModel? Eliminated { get; set; }

    /// <summary>Counts the writes of the document, so that a write made from an older read can be told from the current (ADR-0013).</summary>
    public int Version { get; set; }
    public bool IsDeleted { get; set; }
    public int? DeletedVersion { get; set; }

    /// <summary>
    /// The Participation as it is read (ADR-0005, ADR-0012): each Phase tells the times it shows, which anybody may read, and
    /// how they were recorded, its events with who recorded them and when, only to someone who may see that, which is Staff.
    /// </summary>
    public ParticipationModel ShownTo(bool seesTimeEvents)
    {
        var copy = (ParticipationModel)MemberwiseClone();
        copy.Phases = [.. Phases.Select(x => x.ShownTo(seesTimeEvents))];
        return copy;
    }

    public Participation MapToEntity()
    {
        var competition = Competition.MapToEntity();
        var combination = Combination.MapToEntity();
        var phases = Phases!.Select(x => x.MapToEntity());
        var eliminated = Eliminated?.MapToEntity();
        return new Participation(Category, competition, combination, new(phases), eliminated, EventId, Id, Version);
    }

    void IKrudModel<Participation>.MapFrom(Participation participation)
    {
        Id = participation.Id;
        EventId = participation.EventId;
        Category = participation.Category;
        Competition = CompetitionModel.MapFrom(participation.Competition);
        Combination = CombinationModel.MapFrom(participation.Combination);
        Phases = participation.Phases.Select(PhaseModel.MapFrom).ToArray();
        Eliminated = participation.Eliminated == null ? null : EliminatedModel.MapFrom(participation.Eliminated);
        Version = participation.Version;
    }
}

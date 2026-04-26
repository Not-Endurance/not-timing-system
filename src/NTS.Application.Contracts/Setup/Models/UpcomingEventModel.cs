using Not.Krud.Abstractions;
using NTS.Application.Contracts.Shared;
using NTS.Application.Contracts.Shared.Models;
using NTS.Domain.Enums;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.UpcomingEvents;

namespace NTS.Application.Contracts.Setup.Models;

public class UpcomingEventModel : IDocument, IKrudModel<UpcomingEvent>
{
    public static UpcomingEventModel From(UpcomingEvent @event)
    {
        var model = new UpcomingEventModel();
        model.MapFrom(@event);
        return model;
    }

    public int Id { get; set; } = default!;
    public string TenantId { get; set; } = StorageConstants.DEFAULT_TENANT;
    public string Location { get; set; } = default!;
    public CountryModel Country { get; set; } = default!;
    public string? ShowFeiId { get; set; }
    public string? FeiId { get; set; }
    public string? FeiEventCode { get; set; }
    public CompetitionModel[] Competitions { get; set; } = default!;
    public OfficialModel[] Officials { get; set; } = default!;
    public LoopModel[] Loops { get; set; } = default!;
    public CombinationModel[] Combinations { get; set; } = default!;
    public string Name { get; set; } = default!;

    public UpcomingEvent MapToEntity()
    {
        var country = Country.MapToEntity();
        var competitions = Competitions.Select(x => x.MapToEntity());
        var officials = Officials.Select(x => x.MapToEntity());
        var loops = Loops.Select(x => x.MapToEntity());
        var combinations = Combinations.Select(x => x.MapToEntity());
        return new UpcomingEvent(
            Name,
            Location,
            country,
            ShowFeiId,
            FeiId,
            FeiEventCode,
            competitions,
            officials,
            loops,
            combinations,
            Id
        );
    }

    public void MapFrom(UpcomingEvent @event)
    {
        Id = @event.Id;
        Name = @event.Name;
        Location = @event.Location;
        Country = CountryModel.From(@event.Country);
        ShowFeiId = @event.ShowFeiId;
        FeiId = @event.FeiId;
        FeiEventCode = @event.FeiEventCode;
        Competitions = @event.Competitions.Select(CompetitionModel.MapFrom).ToArray();
        Officials = @event.Officials.Select(OfficialModel.MapFrom).ToArray();
        Loops = @event.Loops.Select(LoopModel.MapFrom).ToArray();
        Combinations = @event.Combinations.Select(CombinationModel.MapFrom).ToArray();
    }
}

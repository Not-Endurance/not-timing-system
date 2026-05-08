using Not.Krud.Abstractions;
using NTS.Application.Contracts.Shared;
using NTS.Application.Contracts.Shared.Models;
using NTS.Domain.Enums;
using NTS.Domain.Setup.Aggregates;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;

namespace NTS.Application.Contracts.Setup.Models;

public class AthleteModel : IDocument, IKrudModel<Athlete>
{
    // TODO: if decide to use this approach integrate AutoMapper with specific mappings to solve duplicating mapping logic
    public static AthleteModel From(Athlete athlete)
    {
        var model = new AthleteModel();
        model.MapFrom(athlete);
        return model;
    }

    public int Id { get; set; }
    public string TenantId { get; set; } = StorageConstants.DEFAULT_TENANT;
    public string[] Names { get; set; } = default!;
    public CountryModel Country { get; set; } = default!;
    public ClubModel? Club { get; set; }
    public string? FeiId { get; set; }
    public UserModel? User { get; set; }

    public void MapFrom(Athlete athlete)
    {
        Id = athlete.Id;
        FeiId = athlete.FeiId;
        Names = athlete.Names.Names;
        Country = CountryModel.From(athlete.Country);
        Club = athlete.Club == null ? null : ClubModel.From(athlete.Club);
        User = athlete.User == null ? null : UserModel.From(athlete.User);
    }

    public Athlete MapToEntity()
    {
        return new Athlete(Names, FeiId, Country?.MapToEntity(), Club?.MapToEntity(), Id, User?.MapToEntity());
    }
}

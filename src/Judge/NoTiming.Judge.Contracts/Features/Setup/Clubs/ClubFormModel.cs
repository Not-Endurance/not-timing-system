using Not.Krud.Models;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Contracts.Features.Setup.Clubs;

public record ClubFormModel : KrudFormModel<Club>
{
    public ClubFormModel()
    {
#if DEBUG
        Name = "Конярче";
#endif
    }

    public string? Name { get; set; }

    protected override Club MapTo()
    {
        return new(Name, Id);
    }

    public override void MapFrom(Club entity)
    {
        Id = entity.Id;
        Name = entity.Name;
    }
}

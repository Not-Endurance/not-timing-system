using Not.Krud.Models;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Contracts.Features.Console.Setup;

public record ClubFormModel : KrudFormModel<Club>
{
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

using Not.Structures;
using NTS.Domain.Enums;

namespace NTS.Domain.Aggregates;

public interface IAthlete : IIdentifiable
{
    Person Names { get; }
    Country Country { get; }
    IClub? Club { get; }
    string? FeiId { get; }
}

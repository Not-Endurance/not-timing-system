using Not.Structures;
using NTS.Domain.Enums;

namespace NTS.Domain.Aggregates;

public interface IAthlete : IIdentifiable
{
    Person Names { get; }
    AthleteCategory Category { get; }
    Country Country { get; }
    IClub? Club { get; }
    string? FeiId { get; }
}

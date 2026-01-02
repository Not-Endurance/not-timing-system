using Not.Structures;
using NTS.Domain.Enums;

namespace NTS.Domain.Aggregates;

public interface IAthlete
{
    Person Names { get; }
    Country Country { get; }
    IClub? Club { get; }
    string? FeiId { get; }
}

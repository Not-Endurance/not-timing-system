using Not.Structures;

namespace NTS.Domain.Aggregates;

public interface IClub : IIdentifiable
{
    string Name { get; }
}

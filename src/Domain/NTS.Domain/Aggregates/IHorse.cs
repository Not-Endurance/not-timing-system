using Not.Structures;

namespace NTS.Domain.Aggregates;

public interface IHorse : IIdentifiable
{
    string Name { get; }
    string? FeiId { get; }
}

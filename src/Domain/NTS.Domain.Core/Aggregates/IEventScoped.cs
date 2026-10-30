namespace NTS.Domain.Core.Aggregates;

public interface IEventScoped
{
    Guid EventId { get; }
}

namespace NoTiming.Domain.Core.Aggregates;

public interface IEventScoped
{
    int EventId { get; }
}

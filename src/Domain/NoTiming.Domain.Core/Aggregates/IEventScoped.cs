namespace NTS.Domain.Core.Aggregates;

public interface IEventScoped
{
    int EventId { get; }
}

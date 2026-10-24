namespace NTS.Nexus.HTTP.Mongo.Repositories;

public interface IEventResetRepository
{
    Task DeleteAllForEvent(Guid eventId);
}

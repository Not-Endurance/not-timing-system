namespace NTS.Nexus.HTTP.Mongo.Repositories;

public interface IEventResetRepository
{
    Task ResetEvent(int eventId);
}

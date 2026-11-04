namespace NTS.Nexus.Warp.Api.Mongo.Repositories;

public interface IEventResetRepository
{
    Task DeleteAllForEvent(int eventId);
}

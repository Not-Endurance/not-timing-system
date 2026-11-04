namespace NoTiming.Api.Mongo.Repositories;

public interface IEventResetRepository
{
    Task DeleteAllForEvent(int eventId);
}

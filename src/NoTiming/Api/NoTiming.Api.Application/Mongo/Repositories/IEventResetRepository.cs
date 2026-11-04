namespace NoTiming.Api.Application.Mongo.Repositories;

public interface IEventResetRepository
{
    Task DeleteAllForEvent(int eventId);
}

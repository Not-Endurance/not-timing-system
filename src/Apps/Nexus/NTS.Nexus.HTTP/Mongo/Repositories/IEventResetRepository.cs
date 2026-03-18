namespace NTS.Nexus.HTTP.Mongo.Repositories;

public interface IEventResetRepository
{
    Task<int?> GetMaxDeletedVersion(int eventId);
    Task SoftDelete(int eventId, int deletedVersion);
}

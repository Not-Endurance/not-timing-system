namespace NoTiming.Application.Contracts.Shared;

/// <summary>Database and collection names that more than one host reads, such as the event bus outbox poller.</summary>
public static class StorageNames
{
    public const string NTS_DATABASE = "nts";
    public const string PARTICIPATIONS_COLLECTION = "event_participations";
}

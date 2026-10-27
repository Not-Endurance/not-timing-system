namespace NoTiming.Api.Features.Snapshots;

/// <summary>What a Snapshot is as a resource, said once for the routes that send it and the documents that carry it.</summary>
internal static class SnapshotDocuments
{
    public const string TYPE = "snapshots";
    public const int MAX_IN_A_GROUP = 100;
}

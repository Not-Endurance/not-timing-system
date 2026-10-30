namespace NTS.Domain.Core.Objects.Snapshots;

/// <summary>What the server answered for one Snapshot of a group that was sent, as far as the rules of a group need it.</summary>
public readonly record struct SnapshotAnswer(bool IsRecorded, int Status, string? ErrorCode);

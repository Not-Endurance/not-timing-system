using NTS.Application.Contracts.Watcher.Models;
using NTS.Domain.Enums;
using NTS.Domain.Objects;
using NTS.Domain.Watcher;

namespace NTS.Tests.Unit.Temporary;

public sealed class NtsUserSessionStateModelTests
{
    [Fact]
    public void Copy_preserves_snapshot_selections_and_history()
    {
        var sentTimestamp = new Timestamp(DateTimeOffset.UtcNow.Date.AddHours(9));
        var model = new NtsUserSessionStateModel
        {
            SnapshotSelections =
            [
                new SnapshotModel
                {
                    Number = 5,
                    Name = "Pending Rider",
                    NameEnglish = "Pending Rider",
                    Timestamp = null,
                },
            ],
            SnapshotHistory =
            [
                SnapshotGroupModel.MapFrom(
                    new SnapshotGroup([new Snapshot(7, "Sent Rider", "Sent Rider", sentTimestamp)], SnapshotType.Arrive)
                ),
            ],
        };

        var copy = model.Copy();

        Assert.NotSame(model.SnapshotSelections, copy.SnapshotSelections);
        Assert.NotSame(model.SnapshotHistory, copy.SnapshotHistory);
        Assert.Equal(5, copy.SnapshotSelections.Single().Number);
        Assert.Null(copy.SnapshotSelections.Single().Timestamp);
        Assert.Equal(7, copy.SnapshotHistory.Single().Entries.Single().Number);
        Assert.Equal(sentTimestamp.ToString(), copy.SnapshotHistory.Single().Entries.Single().Timestamp);

        copy.SnapshotSelections[0].Name = "Changed";

        Assert.Equal("Pending Rider", model.SnapshotSelections.Single().Name);
    }
}

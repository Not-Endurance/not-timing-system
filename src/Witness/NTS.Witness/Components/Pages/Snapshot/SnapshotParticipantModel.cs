using System.Globalization;
using Not.Blazor.CRUD.Forms.Ports;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Objects;
using NTS.Domain.Watcher;

namespace NTS.Witness.Components.Pages.Snapshot;

public class SnapshotParticipantUpdateModel
    : /*ISnapshotParticipantState,*/
    IFormModel<SnapshotParticipant>
{
    public SnapshotParticipantUpdateModel() { }

    public SnapshotParticipantUpdateModel(SnapshotParticipant SnapshotParticipant)
    {
        FromEntity(SnapshotParticipant);
    }

    //int ISnapshotParticipantState.Id => Id ?? default;

    public string? TimestampInput { get; set; }
    public int? Id { get; set; }

    public Timestamp? Timestamp
    {
        get => Parse(TimestampInput);
        set => TimestampInput = ToInputString(value);
    }

    public void FromEntity(SnapshotParticipant entity)
    {
        Id = entity.Number;
        Timestamp = entity.Timestamp;
    }

    Timestamp? Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }
        var dateTime = DateTimeOffset.ParseExact(input, "HH:mm:ss", CultureInfo.InvariantCulture);
        return new Timestamp(dateTime);
    }

    string? ToInputString(Timestamp? timestamp)
    {
        if (timestamp == null)
        {
            return null;
        }
        return timestamp.ToString();
    }
}

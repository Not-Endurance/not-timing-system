using System.Globalization;
using Not.Application.Services;
using NTS.Domain.Core.Aggregates.Participations;

namespace NTS.Blazor.Components.ParticipationTable.Phases;

public class PhaseUpdateModel : IPhaseState, IFormModel<Phase>
{
    public PhaseUpdateModel() { }

    public PhaseUpdateModel(Phase phase)
    {
        FromEntity(phase);
    }

    int IPhaseState.Id => Id ?? default;

    public string? StartTimeInput { get; set; }
    public string? ArriveTimeInput { get; set; }
    public string? PresentTimeInput { get; set; }
    public string? RepresentTimeInput { get; set; }
    public int? Id { get; set; }

    public DateTimeOffset? StartTime
    {
        get => Parse(StartTimeInput);
        set => StartTimeInput = ToInputString(value);
    }
    public DateTimeOffset? ArriveTime
    {
        get => Parse(ArriveTimeInput);
        set => ArriveTimeInput = ToInputString(value);
    }
    public DateTimeOffset? PresentTime
    {
        get => Parse(PresentTimeInput);
        set => PresentTimeInput = ToInputString(value);
    }
    public DateTimeOffset? RepresentTime
    {
        get => Parse(RepresentTimeInput);
        set => RepresentTimeInput = ToInputString(value);
    }

    public void FromEntity(Phase entity)
    {
        Id = entity.Id;
        StartTime = entity.StartTime?.ToDateTimeOffset();
        ArriveTime = entity.ArriveTime?.ToDateTimeOffset();
        PresentTime = entity.PresentTime?.ToDateTimeOffset();
        RepresentTime = entity.RepresentTime?.ToDateTimeOffset();
    }

    DateTimeOffset? Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }
        return DateTimeOffset.ParseExact(input, "HH:mm:ss", CultureInfo.InvariantCulture);
    }

    string? ToInputString(DateTimeOffset? dateTime)
    {
        if (dateTime == null)
        {
            return null;
        }
        return dateTime.Value.ToString("HH:mm:ss");
    }
}

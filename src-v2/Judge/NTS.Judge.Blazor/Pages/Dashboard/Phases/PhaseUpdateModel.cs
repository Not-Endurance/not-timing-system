using Not.Blazor.Ports;
using NTS.Domain.Core.Entities.ParticipationAggregate;
using System.Globalization;

namespace NTS.Judge.Blazor.Pages.Dashboard.Phases;

public class PhaseUpdateModel : IPhaseState, IFormModel<Phase>
{
    public const string TIME_MASK = "00:00:00";

    public PhaseUpdateModel()
    {
    }
    public PhaseUpdateModel(Phase phase)
    {
        FromEntity(phase);
    }

    public string? StartTimeInput { get; set; }
    public string? ArriveTimeInput { get; set; }
    public string? PresentTimeInput { get; set; }
    public string? RepresentTimeInput { get; set; }

    public int Id { get; private set; }
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
        StartTime = entity.StartTime?.DateTime;
        ArriveTime = entity.ArriveTime?.DateTime;
        PresentTime = entity.PresentTime?.DateTime;
        RepresentTime = entity.RepresentTime?.DateTime;
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

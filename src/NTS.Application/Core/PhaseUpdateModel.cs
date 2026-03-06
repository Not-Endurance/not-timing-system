using System.Globalization;
using Not.Krud.Models;
using NTS.Domain.Core.Aggregates.Participations.Entities;

namespace NTS.Application.Core;

public record PhaseUpdateModel : KrudFormModel<Phase>, IPhaseState
{
    public PhaseUpdateModel() { }

    public PhaseUpdateModel(Phase phase)
    {
        MapFrom(phase);
    }

    int IPhaseState.Id => Id ?? default;

    public string? StartTimeInput { get; set; }
    public string? ArriveTimeInput { get; set; }
    public string? PresentTimeInput { get; set; }
    public string? RepresentTimeInput { get; set; }

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

    protected override Phase MapTo()
    {
        throw new NotImplementedException();
    }

    public override void MapFrom(Phase entity)
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

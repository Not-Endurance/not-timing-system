using NoTiming.Domain.Core.Aggregates.Participations;

namespace NoTiming.Application.Contracts.Core.Commands;

public class FailToQualifyModel
{
    public FailToQualifyCode[] Codes { get; set; } = [];
    public string? Reason { get; set; }
}

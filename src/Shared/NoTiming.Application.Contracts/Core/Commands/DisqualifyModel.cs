using NoTiming.Domain.Core.Aggregates.Participations;

namespace NoTiming.Application.Contracts.Core.Commands;

public class DisqualifyModel
{
    public DisqualifyCode[] Codes { get; set; } = [];
    public string? Reason { get; set; }
}

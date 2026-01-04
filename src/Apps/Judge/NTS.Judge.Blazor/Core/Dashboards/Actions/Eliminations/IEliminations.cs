using NTS.Domain.Core.Aggregates.Participations;

namespace NTS.Judge.Blazor.Core.Dashboards.Actions.Eliminations;

public interface IEliminations : IParticipationContext
{
    Task Withdraw();
    Task Retire();
    Task FinishNotRanked(string reason);
    Task Disqualify(DisqualifyCode[] dqCodes, string? reason);
    Task FailToQualify(FailToQualifyCode[] ftqCodes, string? reason);
    Task RestoreQualification();
}

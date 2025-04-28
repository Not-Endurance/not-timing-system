using NTS.Domain.Core.Aggregates.Participations;

namespace NTS.Judge.Blazor.Core.Dashboards.Actions.Eliminations;

public interface IEliminations : IParticipationContext
{
    Task Withdraw();
    Task Retire();
    Task FinishNotRanked(string reason);
    Task Disqualify(DqCode[] dqCodes, string? reason);
    Task FailToQualify(FtqCode[] ftqCodes, string? reason);
    Task RestoreQualification();
}

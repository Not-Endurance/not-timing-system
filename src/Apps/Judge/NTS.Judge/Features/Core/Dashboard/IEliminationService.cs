using NTS.Application.Core;
using NTS.Domain.Core.Aggregates.Participations;

namespace NTS.Judge.Features.Core.Dashboard;

public interface IEliminationService
{
    bool IsEliminated { get; }
    Task Withdraw();
    Task Retire();
    Task FinishNotRanked(string reason);
    Task Disqualify(DisqualifyCode[] dqCodes, string? reason);
    Task FailToQualify(FailToQualifyCode[] ftqCodes, string? reason);
    Task RestoreQualification();
}

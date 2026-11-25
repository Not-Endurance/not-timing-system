using NoTiming.Application.Contracts.Notices;

namespace NoTiming.EventBus.Contracts.Features.Judge.Procedures;

public interface IJudgeClientProcedures
{
    Task ParticipationChanged(ParticipationChangedNotice notice);
}

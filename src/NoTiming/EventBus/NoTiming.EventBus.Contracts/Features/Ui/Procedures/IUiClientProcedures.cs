using NoTiming.Application.Contracts.Notices;

namespace NoTiming.EventBus.Contracts.Features.Ui.Procedures;

public interface IUiClientProcedures
{
    Task ParticipationChanged(ParticipationChangedNotice notice);
}

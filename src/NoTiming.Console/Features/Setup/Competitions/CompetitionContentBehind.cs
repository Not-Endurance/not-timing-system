namespace NoTiming.Console.Features.Setup.Competitions;

/// <summary>The page of one Competition of the Setup: its form, its Phases and its Participations.</summary>
public class CompetitionContentBehind : SetupFormContent<CompetitionFormModel>
{
    protected string PhaseRoute => Routes.Of(ConsoleRoutes.PHASE_PAGE, EventId);
    protected string ParticipationRoute => Routes.Of(ConsoleRoutes.PARTICIPATION_PAGE, EventId);
}

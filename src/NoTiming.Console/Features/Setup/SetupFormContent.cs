using Not.Krud.Abstractions;

namespace NoTiming.Console.Features.Setup;

/// <summary>A page that edits one child of the Setup of an Event (#646): a Competition, a Loop, a Combination and the like.</summary>
public class SetupFormContent<T> : ConsoleFormContent<T>
    where T : IKrudFormModel, new()
{
    protected override string BackRoute => Routes.Of(ConsoleRoutes.EVENT_SETUP_PAGE, EventId);

    [Parameter]
    public Guid EventId { get; set; }
}

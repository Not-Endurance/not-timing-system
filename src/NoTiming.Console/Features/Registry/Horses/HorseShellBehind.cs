using Not.Krud.Blazor.Components.Abstractions;

namespace NoTiming.Console.Features.Registry.Horses;

public class HorseShellBehind : KrudShell<HorseFormModel>
{
    [Inject]
    IConsoleSameRows SameRows { get; set; } = default!;

    /// <summary>The rows that the Horse being registered looks like, which the form warns of.</summary>
    protected IReadOnlyList<SameRow> Same { get; private set; } = [];

    protected async Task Check()
    {
        try
        {
            Same = await SameRows.OfHorse(Model);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}

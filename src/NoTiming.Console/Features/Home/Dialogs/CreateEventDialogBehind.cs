using Not.Blazor.Dialogs.Abstractions;

namespace NoTiming.Console.Features.Home.Dialogs;

public class CreateEventDialogBehind : NDialog<NewEvent>
{
    protected NewEvent Model { get; } = new();
    protected bool CanCreate => !string.IsNullOrWhiteSpace(Model.Name) && !string.IsNullOrWhiteSpace(Model.Location);

    protected Task Create()
    {
        return CanCreate ? ConfirmDialog(Model) : Task.CompletedTask;
    }
}

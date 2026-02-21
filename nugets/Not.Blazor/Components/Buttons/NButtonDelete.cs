using MudBlazor;

namespace Not.Blazor.Components.Buttons;

public class NButtonDelete : NButtonPrimary
{
    public NButtonDelete()
    {
        StartIcon = Icons.Material.Filled.Delete;
        Color = Color.Error;
        Text = Delete_string;
    }
}

using MudBlazor;

namespace Not.Blazor.Components;

public class NButtonUpdate : NButtonPrimary
{
    public NButtonUpdate()
    {
        StartIcon = Icons.Material.Filled.Create;
        Text = Update_string;
    }
}

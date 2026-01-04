using MudBlazor;

namespace Not.Blazor.Components;

public class NButtonCreate : NButtonPrimary
{
    public NButtonCreate()
    {
        StartIcon = Icons.Material.Filled.Add;
        Text = Create_string;
    }
}

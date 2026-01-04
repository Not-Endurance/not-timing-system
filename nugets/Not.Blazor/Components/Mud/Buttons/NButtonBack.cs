using MudBlazor;

namespace Not.Blazor.Components;

public class NButtonBack : NButtonSecondary
{
    public NButtonBack()
    {
        StartIcon = Icons.Material.Outlined.ArrowBackIos;
        Text = Back_string;
    }
}

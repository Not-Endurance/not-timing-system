using MudBlazor;

namespace NC.Blazor.Components.Buttons;

public class NButtonBack : NButtonSecondary
{
    public NButtonBack()
    {
        StartIcon = Icons.Material.Outlined.ArrowBackIos;
        Text = Back_string;
    }
}

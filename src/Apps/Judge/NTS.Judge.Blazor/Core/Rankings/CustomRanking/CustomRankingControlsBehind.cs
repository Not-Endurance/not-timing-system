using MudBlazor;
using Not.Blazor.Components;
using Not.Blazor.CRUD.Forms.Components;
using NTS.Judge.Features.Core.Rankings.CustomRankings;

namespace NTS.Judge.Blazor.Core.Rankings.CustomRanking;

public class CustomRankingControlsBehind : NForm<CustomRankingModel>
{
    protected MudTextField<string?> NameField = default!;

    public override void RegisterValidationInjectors()
    {
        RegisterInjector(nameof(CustomRankingModel.Name), () => NameField);
    }
}

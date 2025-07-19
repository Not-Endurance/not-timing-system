using MudBlazor;
using Not.Blazor.Components;
using Not.Blazor.CRUD.Forms.Components;

namespace NTS.Judge.Blazor.Core.Rankings.CustomRanking;

public class CustomRankingControlsBehind : NForm<CustomRankingModel>
{
    protected MudTextField<string?> NameField = default!;
    protected NSelect<CompetitionRuleset?> RulesetField = default!;
    protected NSelect<CompetitionType?> TypeField = default!;
    protected NSelect<ParticipationCategory?> CategoryField = default!;

    public override void RegisterValidationInjectors()
    {
        RegisterInjector(nameof(CustomRankingModel.Name), () => NameField);
        RegisterInjector(nameof(CustomRankingModel.Ruleset), () => RulesetField);
        RegisterInjector(nameof(CustomRankingModel.Type), () => TypeField);
        RegisterInjector(nameof(CustomRankingModel.Category), () => CategoryField);
    }
}

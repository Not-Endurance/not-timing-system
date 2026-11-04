using Not.Application.Authentication.User;
using Not.Application.Behinds.Adapters;
using NTS.Domain.Aggregates;

namespace NoTiming.Ui.Contracts.Features.Profile;

public interface IUiProfileContext : IStatefulService
{
    NUserModel? User { get; }
    bool RequiresProfileCompletion { get; }
    string WelcomeName { get; }
    UiProfileFormModel CreateFormModel();
    Task<IEnumerable<Country>> SearchCountries(string term, CancellationToken ct);
    Task<NUserModel?> Save(UiProfileFormModel model);
}

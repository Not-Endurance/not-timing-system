using NC.Application.Authentication.User;
using NC.Application.Behinds.Adapters;
using NoTiming.Application.Contracts.Account;
using NoTiming.Domain.Aggregates;

namespace NoTiming.Ui.Contracts.Features.Profile;

public interface IUiProfileContext : IStatefulService
{
    NUserModel? User { get; }
    NoTimingProfile? Profile { get; }
    bool RequiresProfileCompletion { get; }
    string WelcomeName { get; }
    UiProfileFormModel CreateFormModel();
    Task<IEnumerable<Country>> SearchCountries(string term, CancellationToken ct);
    Task<NUserModel?> Save(UiProfileFormModel model);
}

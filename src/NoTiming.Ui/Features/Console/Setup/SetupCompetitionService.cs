using Not.Application.CRUD.Ports;
using Not.Injection;
using Not.Krud.Services;
using NTS.Contracts.Features.Console.Setup;
using NTS.Domain.Setup.Aggregates.ConfigureEvents;

namespace NoTiming.Ui.Features.Console.Setup;

/// <summary>The Competitions of the Setup that is open: the form and the list that the Krud pages of the Setup use (#646).</summary>
public class SetupCompetitionService : KrudServiceBase<Competition, CompetitionFormModel>, ITransient
{
    public SetupCompetitionService(IRepository<Competition> competitions)
        : base(competitions, []) { }
}

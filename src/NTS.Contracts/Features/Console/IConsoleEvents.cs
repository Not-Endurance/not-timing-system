using Not.Application.Behinds.Adapters;
using NTS.Domain.Setup.Services.StartValidation;

namespace NTS.Contracts.Features.Console;

/// <summary>
/// The Events of the Console (#646): the Setups the person sees (a Tenant Root's are the Tenant's, a Main Operator's the
/// Events it runs), each at its stage with what the person may do, and the things that are done to an Event as a whole:
/// making it, assigning and handing over its Main Operator, starting it and resetting it. It follows the account.
/// </summary>
public interface IConsoleEvents : IStatefulService
{
    IReadOnlyList<ConsoleEvent> Events { get; }

    /// <summary>Whether the person may make an Event in the Tenant they act in.</summary>
    bool CanCreate { get; }

    /// <summary>Makes an Event in the Tenant the person acts in, with the person as its Main Operator.</summary>
    Task<ConsoleEvent> Create(string name, string location, string? feiShowId);

    /// <summary>What is missing from the Setup for the Event to start: nothing when it can.</summary>
    Task<IReadOnlyList<StartValidationIssue>> Validate(Guid setupId);

    /// <summary>Starts the Event from its Setup. The Api's own refusal, with what it says, is thrown as a domain error.</summary>
    Task Start(Guid setupId);

    /// <summary>Gives the Event back as its Setup, with what it made and what was kept for it removed.</summary>
    Task Reset(Guid eventId);

    Task AssignMainOperator(Guid eventId, Guid accountId);

    Task HandOver(Guid eventId, Guid accountId);

    /// <summary>The accounts of the Tenant whose name holds the text, at most ten.</summary>
    Task<IReadOnlyList<AccountMatch>> FindAccounts(string text);
}

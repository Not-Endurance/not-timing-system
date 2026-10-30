using Not.Blazor.Dialogs.Abstractions;
using Not.Domain.Exceptions;
using Not.Structures;
using NTS.Domain.Access;
using NTS.Domain.Enums;

namespace NoTiming.Console.Features.Setup.Access;

/// <summary>
/// Links a person to an Event as an Operator or as an Official of a role, by their exact email (#646, ADR-0012). The Api
/// says whether it took the request, and the dialog stays open with what was typed when it did not, and says what the Api says.
/// </summary>
public class LinkAccountDialogBehind : NDialog<GrantRow>
{
    [Inject]
    IConsoleGrants Grants { get; set; } = default!;

    protected GrantKind Kind { get; set; } = GrantKind.Operator;
    protected OfficialRole? Role { get; set; } = OfficialRole.Steward;
    protected string Email { get; set; } = string.Empty;
    protected string? Refusal { get; private set; }
    protected bool IsOfficial => Kind == GrantKind.Official;
    protected bool CanLink => !string.IsNullOrWhiteSpace(Email) && (!IsOfficial || Role.HasValue);

    protected IEnumerable<NotListModel<GrantKind>> Kinds { get; } =
        [new(GrantKind.Operator, Operator_string), new(GrantKind.Official, Official_string)];

    [Parameter, EditorRequired]
    public Guid EventId { get; set; }

    protected async Task Link()
    {
        if (!CanLink)
        {
            return;
        }

        try
        {
            Refusal = null;
            await ConfirmDialog(await Grants.Link(EventId, Kind, IsOfficial ? Role : null, Email));
        }
        catch (DomainException ex)
        {
            Refusal = ex.Message;
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}

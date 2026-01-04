using NTS.Warp.ACL.Entities.Athletes;
using NTS.Warp.ACL.Enums;

namespace NTS.Warp.ACL.Models;

internal class EmsAthleteState : IEmsAthleteState
{
    public string? FeiId { get; set; }
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string? Club { get; set; }
    public EmsCategory Category { get; set; }
    public int Id { get; set; }
}

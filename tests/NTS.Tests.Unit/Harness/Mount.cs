using Not.Application.Authentication.User;
using Not.Domain;

namespace NTS.Tests.Unit.Harness;

internal sealed class Mount : Entity
{
    public Mount(Guid? id = null)
        : base(id) { }

    public override string ToString()
    {
        return nameof(Mount);
    }
}

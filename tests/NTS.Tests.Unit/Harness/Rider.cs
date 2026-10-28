using Not.Application.Authentication.User;
using Not.Domain;

namespace NTS.Tests.Unit.Harness;

internal class Rider : Entity
{
    public Rider(Guid? id = null)
        : base(id) { }

    public override string ToString()
    {
        return nameof(Rider);
    }
}

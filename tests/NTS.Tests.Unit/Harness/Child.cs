using Not.Domain;
using Not.Krud.Abstractions;
using Not.Krud.Services;

namespace NTS.Tests.Unit.Harness;

internal sealed class Child : Entity
{
    readonly string _name;

    public Child(string name)
        : base(null)
    {
        _name = name;
    }

    public override string ToString()
    {
        return _name;
    }
}

using Not.Injection;

namespace NTS.Judge.Features.Setup.Services;

internal class AccountContext : IAccountContext, ISingleton
{
    public Guid Id { get; } = Guid.Parse("ec6d8f0d-ecad-4fb6-a10f-fdb190dc0cd4");
}

public interface IAccountContext
{
    Guid Id { get; }
}

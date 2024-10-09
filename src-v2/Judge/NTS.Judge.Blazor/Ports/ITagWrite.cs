using Not.Injection;
using NTS.Domain.Setup.Entities;

namespace NTS.Judge.Blazor.Ports;
public interface ITagWrite : ISingletonService
{
    Task<Tag> WriteAsync(int number);
}

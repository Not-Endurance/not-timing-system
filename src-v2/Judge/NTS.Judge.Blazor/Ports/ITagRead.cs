using Not.Injection;
using NTS.Domain.Objects;

namespace NTS.Judge.Blazor.Ports;
public interface ITagRead : ISingletonService
{
    Task<IEnumerable<RfidSnapshot>> ReadTags();
}

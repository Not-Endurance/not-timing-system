using Not.Injection;
using NTS.Domain.Objects;

namespace NTS.Judge.Blazor.Ports;
public interface IRfidTagReaderBehind : ISingletonService
{
    Task StartReading(bool readFlag);
    IEnumerable<Snapshot> ProcessTags();

    //void ProcessTags();
}

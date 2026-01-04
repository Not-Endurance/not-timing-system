using Not.Injection;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Judge.Blazor.Setup.Combinations.RFID;

public interface IRfidWriterBehind : ITransient
{
    Task<Tag> WriteTag(int number);
}

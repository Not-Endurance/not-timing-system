using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Not.Exceptions;
using Not.Safe;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Blazor.Setup.Combinations.RFID;
using NTS.Judge.RFID;
using Vup.reader;
using Tag = NTS.Domain.Setup.Aggregates.Tag;

namespace NTS.Judge.Core.Behinds.Adapters;

public class RfidWriterBehind : IRfidWriterBehind
{
    VupVD67Controller _vD67Controller;

    public RfidWriterBehind()
    {
        _vD67Controller = new VupVD67Controller(TimeSpan.FromMilliseconds(100));
    }

    public async Task<Tag> WriteTag(int number)
    {
        Task<Tag> action() => SafeWriteTag(number);
        var tagResult = await SafeHelper.Run(action);
        GuardHelper.ThrowIfDefault(tagResult);
        return tagResult;
    }

    async Task<Tag> SafeWriteTag(int number)
    {
        Tag function()
        {
            var data = _vD67Controller.Read();
            var id = data.Substring(9);
            var tag = new Tag(id, number);
            var writeData = tag.PrepareToWrite();
            _vD67Controller.Write(writeData);
            return tag;
        }
        var tag = await Task.Run(function);
        return tag;
    }
}

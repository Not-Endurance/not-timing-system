using NTS.Domain.Setup.Entities;
using NTS.Judge.Blazor.Ports;
using NTS.Judge.HardwareControllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTS.Judge.Adapters.Behinds;

public class RfidTagWriterBehind : IRfidTagWriterBehind
{
    private VupVD67Controller VD67Controller;

    public RfidTagWriterBehind()
    {
        VD67Controller = new VupVD67Controller(TimeSpan.FromMilliseconds(100));
    }

    public async Task<Tag> WriteTag(int number)
    {
        var tag = await Task.Run(() =>
        {
            var data = VD67Controller.Read();
            var id = data.Substring(9);
            var tag = new Tag(id, number);
            VD67Controller.Write(tag.PrepareToWrite());
            return tag;
        });
        return tag;
    }
}
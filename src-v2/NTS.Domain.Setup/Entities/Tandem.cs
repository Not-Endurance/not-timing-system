using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NTS.Domain.Setup.Entities;
public class Tandem: DomainEntity, ISummarizable
{
    public string Summarize()
    {
        var summary = new Summarizer(this);
        return summary.ToString();
    }
    public override string ToString()
    {
        var sb = new StringBuilder();
        //sb.Append($"{LocalizationHelper.Get(this.Type)}, {"Loops".Localize()}: {this.Loops.Count}, {"Starters".Localize()}: {this.Contestants.Count}, ");
        //sb.Append($"{"Start".Localize()}: {this.StartTime.ToString("f", CultureInfo.CurrentCulture)}");
        return sb.ToString();
    }
}

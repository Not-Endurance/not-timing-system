using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace NTS.Domain.Setup.Entities;
public class Contestant : DomainEntity, ISummarizable
{
    public static Contestant Create(DateTimeOffset newStart) => new(newStart);
    public static Contestant Update(DateTimeOffset newStart) => new(newStart);

    private List<Tandem> _tandems = new();

    [JsonConstructor]
    private Contestant(DateTimeOffset newStartTime)
    {

        if (newStartTime.DateTime.CompareTo(DateTime.Today) < 0)
        {
            throw new DomainException(nameof(DateTime), "Start time cannot be in the past");
        }

        StartTimeOverride = newStartTime;
    }
    public Tandem ContestantHorsePair {  get; private set; }

    public DateTimeOffset StartTimeOverride { get; private set; }

    public IReadOnlyList<Tandem> Tandems
    {
        get => _tandems.AsReadOnly();
        private set => _tandems = value.ToList();
    }

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

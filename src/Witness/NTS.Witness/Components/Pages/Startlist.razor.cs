using NTS.Domain.Core.Objects.Startlists;
using NTS.Domain.Objects;

namespace NTS.Witness.Components.Pages;

public partial class Startlist
{
    List<StartlistEntry> _startlistEntries = [];

    protected override void OnInitialized()
    {
        var startlistEntry1 = new StartlistEntry(new Person(["Todomir", "Stroinov"]), 24, 2, 20, DateTimeOffset.Now.AddMinutes(12));
        var startlistEntry2 = new StartlistEntry(new Person(["Antonia", "Kraiselska"]), 36, 2, 20, DateTimeOffset.Now.AddMinutes(9));
        _startlistEntries.Add(startlistEntry1);
        _startlistEntries.Add(startlistEntry2);
    }
}


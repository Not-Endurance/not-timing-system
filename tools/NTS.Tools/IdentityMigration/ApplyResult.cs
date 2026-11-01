using NTS.Domain.Setup.Services.Identities;

namespace NTS.Tools.IdentityMigration;

/// <summary>How an apply is run (#637).</summary>
public sealed class ApplyOptions
{
    /// <summary>The instant an Event is told Live or Historic at: the clock of the machine unless a test gives one.</summary>
    public required DateTimeOffset Now { get; init; }

    /// <summary>Called when a step is done; a test stops the run by throwing from it, which a run that is stopped for another reason looks like.</summary>
    public Func<string, Task>? AfterStep { get; init; }

    /// <summary>Called with the id of a Ranking just before its placings are written; a test stores a rank there, as a second writer would.</summary>
    public Func<Guid, Task>? BeforeRankingWritten { get; init; }

    /// <summary>Called with the plan when a run makes one, before anything is written or refused: the command line writes the report and the decisions from it.</summary>
    public Func<IdentityPlan, Task>? PlanMade { get; init; }

    /// <summary>How many documents a read asks for.</summary>
    public int PageSize { get; init; } = ThrottledReads.PAGE_SIZE;

    /// <summary>How a throttled request waits; the clock's delay unless a test gives its own.</summary>
    public Func<TimeSpan, Task>? Wait { get; init; }
}

/// <summary>What an apply would do, as the dry run can tell it (#637): what would stop it, the Rankings it would finalise, and the verification of the planned result.</summary>
public sealed class PlannedCutover
{
    public PlannedCutover(
        IReadOnlyList<string> refusals,
        IReadOnlyList<HistoricRanking> rankings,
        IReadOnlyList<VerificationCheck> checks
    )
    {
        Refusals = refusals;
        Rankings = rankings;
        Checks = checks;
    }

    /// <summary>What would refuse an apply, each as a sentence.</summary>
    public IReadOnlyList<string> Refusals { get; }

    /// <summary>The Rankings of the Events that have ended, and where their placings are.</summary>
    public IReadOnlyList<HistoricRanking> Rankings { get; }

    public IReadOnlyList<VerificationCheck> Checks { get; }
}

/// <summary>One thing the verification looks at, and whether it holds (#637).</summary>
public sealed class VerificationCheck
{
    public VerificationCheck(string name, bool passed, IReadOnlyList<string> details)
    {
        Name = name;
        Passed = passed;
        Details = details;
    }

    public string Name { get; }
    public bool Passed { get; }

    /// <summary>What does not hold, each as a sentence, or what was left alone and told; a few at most.</summary>
    public IReadOnlyList<string> Details { get; }
}

/// <summary>What the finalisation of the Rankings of the Historic Events found and did (#637, #640).</summary>
public sealed class RankingsReport
{
    /// <summary>The Rankings whose ranks were stored.</summary>
    public List<Guid> Finalised { get; } = [];

    /// <summary>The Rankings that every entry of holds a rank already: they are left as they are.</summary>
    public List<Guid> AlreadyFinal { get; } = [];

    /// <summary>The Rankings that hold only some of their ranks, which nothing here produces: they are left alone and told.</summary>
    public List<Guid> SomePlacingsStored { get; } = [];

    /// <summary>The Rankings the domain refused to read or to finalise, such as one that counts a Participation that is not there: they are left alone and told.</summary>
    public List<Guid> Refused { get; } = [];
}

/// <summary>What an apply did, or why it did not (#637).</summary>
public sealed class ApplyResult
{
    /// <summary>What stopped the run before anything was written, or what stopped it half way: each as a sentence.</summary>
    public List<string> Refusals { get; } = [];

    /// <summary>The steps that were done, in order.</summary>
    public List<string> Steps { get; } = [];

    /// <summary>Whether the run finished a run that had stopped, from the plan that run had kept.</summary>
    public bool Resumed { get; set; }

    /// <summary>How many documents of each collection were written, converted or made.</summary>
    public SortedDictionary<string, int> Written { get; } = new(StringComparer.Ordinal);

    /// <summary>The Setup rows that were deleted because they are another row's duplicate.</summary>
    public int RowsMerged { get; set; }

    /// <summary>The Setup rows that were made for an identity that had none.</summary>
    public int RowsMade { get; set; }

    public RankingsReport Rankings { get; set; } = new();

    public List<VerificationCheck> Checks { get; } = [];

    /// <summary>Whether it was not refused and every check of the verification holds.</summary>
    public bool Succeeded => Refusals.Count == 0 && Checks.Count > 0 && Checks.All(x => x.Passed);
}

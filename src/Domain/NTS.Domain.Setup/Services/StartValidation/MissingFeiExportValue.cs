namespace NTS.Domain.Setup.Services.StartValidation;

/// <summary>
/// A value of the FEI export that a Setup lacks, and whose it is: the competition's, or the Horse's or the Athlete's of a
/// combination that rides in it (<c>#101, Name</c>). The show ID is the Event's and has no subject.
/// </summary>
public sealed record MissingFeiExportValue
{
    public MissingFeiExportValue(FeiExportValue value, string? subject)
    {
        Value = value;
        Subject = subject;
    }

    public FeiExportValue Value { get; }
    public string? Subject { get; }
}

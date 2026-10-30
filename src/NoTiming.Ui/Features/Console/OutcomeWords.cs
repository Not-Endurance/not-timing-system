namespace NoTiming.Ui.Features.Console;

/// <summary>The outcome of a time as words (#646), as the person is told it: <c>RejectedDuplicateArrive</c> is "Rejected Duplicate Arrive".</summary>
public static class OutcomeWords
{
    public static string Of(string name)
    {
        var words = new List<char>();
        foreach (var letter in name)
        {
            if (char.IsUpper(letter) && words.Count > 0)
            {
                words.Add(' ');
            }

            words.Add(letter);
        }

        return new string([.. words]);
    }
}

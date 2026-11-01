using System.Text;

namespace NTS.Tools.IdentityMigration;

/// <summary>What <see cref="OutputFiles.SaveDecisions"/> did with the decisions file.</summary>
public enum DecisionsSaved
{
    /// <summary>There was nothing to add to what the owner had written.</summary>
    Unchanged = 1,

    Written = 2,

    /// <summary>The file was changed while the run read the database, so it was left as it is now, and the run is to be made again.</summary>
    Kept = 3,
}

/// <summary>
/// The files <c>migrate-identities</c> writes (#636). They hold real names, so they are written where the caller says, in UTF-8, and
/// the decisions file, which the owner edits, is never written over a change the owner has made since the run read it.
/// </summary>
public static class OutputFiles
{
    /// <summary>Writes the text to the file, making the folders on the way.</summary>
    public static async Task Write(string path, string text)
    {
        if (Path.GetDirectoryName(Path.GetFullPath(path)) is { } folder)
        {
            Directory.CreateDirectory(folder);
        }

        await File.WriteAllTextAsync(path, text, new UTF8Encoding(false));
    }

    /// <summary>
    /// Writes the decisions the run came to, unless they are what the file says already, or the file is not what the run read at its
    /// start: a run takes a minute, and a decision the owner saved in that minute is theirs to keep.
    /// </summary>
    /// <param name="path">The decisions file.</param>
    /// <param name="read">What the file said when the run began, none if there was no file.</param>
    /// <param name="text">What the run would write.</param>
    public static async Task<DecisionsSaved> SaveDecisions(string path, string? read, string text)
    {
        if (text == read)
        {
            return DecisionsSaved.Unchanged;
        }

        var now = File.Exists(path) ? await File.ReadAllTextAsync(path) : null;
        if (now != read)
        {
            return DecisionsSaved.Kept;
        }

        await Write(path, text);
        return DecisionsSaved.Written;
    }
}

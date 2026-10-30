namespace NTS.Contracts.Branding;

/// <summary>Which of the two places on the head of a document a logo is for.</summary>
public enum LogoPosition
{
    Left = 0,
    Right = 1,
}

/// <summary>Whose a logo is that an Event is printed with: its own, or the one of its Tenant that it has none in place of.</summary>
public enum LogoSource
{
    Tenant = 0,
    Event = 1,
}

/// <summary>A logo as the Api tells it (#646): an image, and whose it is when it is the one an Event is printed with.</summary>
public sealed class Logo
{
    public Logo(string contentType, byte[] content, LogoSource? source = null)
    {
        ContentType = contentType;
        Content = content;
        Source = source;
    }

    public string ContentType { get; }
    public byte[] Content { get; }

    /// <summary>Whose it is: set for what an Event is printed with, and none for what a Tenant keeps.</summary>
    public LogoSource? Source { get; }

    public int Size => Content.Length;

    /// <summary>The image as an address a page can show and print without asking the host for it.</summary>
    public string DataUrl => $"data:{ContentType};base64,{Convert.ToBase64String(Content)}";
}

/// <summary>The two logos of a Tenant, or of what an Event is printed with: a position that has none is none.</summary>
public sealed class Logos
{
    public Logos(Logo? left, Logo? right)
    {
        Left = left;
        Right = right;
    }

    public static Logos None { get; } = new(null, null);
    public Logo? Left { get; }
    public Logo? Right { get; }

    public Logo? At(LogoPosition position)
    {
        return position == LogoPosition.Left ? Left : Right;
    }
}

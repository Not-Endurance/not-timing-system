using Not.Exceptions;

namespace Not.Application.CRUD.Ports;

/// <summary>
/// The error every repository throws when its store did not do what was asked (ADR-0017). A caller catches this one type
/// whichever store answered. It is told the words to tell a person, the stable code the store gave, and the kind of failure,
/// which is what a caller branches on: whether to try again, whether the person must be asked who they are, or whether the
/// same request would get the same answer.
/// </summary>
public class RepositoryException : NotException
{
    public RepositoryException(RepositoryErrorKind kind, string message, string? code = null, Exception? inner = null)
        : base(message, inner)
    {
        Kind = kind;
        Code = code;
    }

    public RepositoryErrorKind Kind { get; }

    /// <summary>The stable code the store gave the failure, such as <c>id-taken</c>; none when it gave none.</summary>
    public string? Code { get; }
}

public enum RepositoryErrorKind
{
    /// <summary>No answer came, or the answer asks for another try: the same request may be sent again.</summary>
    Unavailable,

    /// <summary>The caller is not known or may not do this: the account is asked again.</summary>
    NotAllowed,

    /// <summary>The store refused: the same request gets the same answer.</summary>
    Refused,
}

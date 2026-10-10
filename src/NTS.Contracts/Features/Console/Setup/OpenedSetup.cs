namespace NTS.Contracts.Features.Console.Setup;

/// <summary>The Setup of an Event in the hands of the person who runs it, and what they may still do about it.</summary>
public sealed class OpenedSetup
{
    public OpenedSetup(ConfigureEventFormModel model, bool canEdit, bool canLinkAccounts)
    {
        Model = model;
        CanEdit = canEdit;
        CanLinkAccounts = canLinkAccounts;
    }

    /// <summary>The form of the Event, which holds the Setup as it was read.</summary>
    public ConfigureEventFormModel Model { get; }

    /// <summary>Whether the Setup may be changed: it may until the Event starts, and is only read from then on.</summary>
    public bool CanEdit { get; }

    /// <summary>Whether people may still be linked to the Officials and Operators of the Event: until it ends.</summary>
    public bool CanLinkAccounts { get; }
}

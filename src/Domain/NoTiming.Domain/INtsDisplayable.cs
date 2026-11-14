namespace NoTiming.Domain;

public interface INoTimingDisplayable
{
    string GetDisplayName(bool englishNames = false, CultureInfo? culture = null);
}

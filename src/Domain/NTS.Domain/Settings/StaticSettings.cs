using NTS.Domain.Aggregates;
using NTS.Domain.Enums;

namespace NTS.Domain.Settings;

public static class StaticSettings
{
    internal static Setting? Instance { get; set; } // TODO: instantiate
    public static Country? SelectedCountry => Instance?.Country;
    public static DetectionMode? DetectionMode => Instance?.DetectionMode;
}

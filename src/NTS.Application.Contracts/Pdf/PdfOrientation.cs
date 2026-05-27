using System.ComponentModel.DataAnnotations;
using NTS.Localization.Resources;

namespace NTS.Application.Contracts.Pdf;

public enum PdfOrientation
{
    [Display(Name = "Portrait_string", ResourceType = typeof(LocalizedStrings))]
    Portrait,

    [Display(Name = "Landscape_string", ResourceType = typeof(LocalizedStrings))]
    Landscape,
}

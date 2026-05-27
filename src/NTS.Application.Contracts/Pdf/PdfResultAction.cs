using System.ComponentModel.DataAnnotations;
using NTS.Localization.Resources;

namespace NTS.Application.Contracts.Pdf;

public enum PdfResultAction
{
    [Display(Name = "Print_string", ResourceType = typeof(LocalizedStrings))]
    Print,

    [Display(Name = "Download_results_string", ResourceType = typeof(LocalizedStrings))]
    Download,
}

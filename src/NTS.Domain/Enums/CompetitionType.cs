using System.ComponentModel.DataAnnotations;

namespace NTS.Domain.Enums;

public enum CompetitionType // TODO: Use DisplayAttribute
{
    [Display(Name = "Qualification")]
    Qualification = 1,

    [Display(Name = "Star Level")]
    Star = 2,

    [Display(Name = "Championship")]
    Championship = 3,
}

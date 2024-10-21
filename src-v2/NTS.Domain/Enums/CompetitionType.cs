using System.ComponentModel.DataAnnotations;

namespace NTS.Domain.Enums;

public enum CompetitionType
{
    [Display(Name = "Qualification")]
    Qualification = 1,
    [Display(Name = "Star Level")]
    Star = 2
}
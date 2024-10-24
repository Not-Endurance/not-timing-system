using Not.Blazor.Ports;
using NTS.Domain.Setup.Entities;

namespace NTS.Judge.Blazor.Pages.Setup.Horses;

public class HorseFormModel : IFormModel<Horse>
{
    public HorseFormModel() 
    {
#if DEBUG
        Name = "Хан Аспарух";
#endif
    }

    public int Id { get; set; }
    public string? FeiId { get; set; }
    public string? Name { get; set; }

    public void FromEntity(Horse horse)
    {
        Id = horse.Id;
        FeiId = horse.FeiId;
        Name = horse.Name;
    }
}

namespace NoTiming.Ui.Components.Arrivelist;

/// <summary>
/// Which of the Arrivelist's two estimate models the table renders. Presentation-only - the domain
/// computes both sets for every Participation and knows nothing about this selection.
/// </summary>
public enum ArrivelistEstimateMode
{
    Fei = 1,
    Regional = 2,
    Both = 3,
}

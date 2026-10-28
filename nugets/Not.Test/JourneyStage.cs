namespace Not.Test;

/// <summary>One numbered stage of a journey: its name, and what it does.</summary>
public sealed class JourneyStage
{
    public JourneyStage(int number, string name, Func<Task> run)
    {
        Number = number;
        Name = name;
        Run = run;
    }

    public int Number { get; }
    public string Name { get; }
    public Func<Task> Run { get; }
}

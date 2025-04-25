namespace Not.SystemProcess;

public class ProcessTetherContext
{
    public ProcessTetherContext(string parentPid)
    {
        ParentProcessId = int.Parse(parentPid);
    }

    public int ParentProcessId { get; }
}

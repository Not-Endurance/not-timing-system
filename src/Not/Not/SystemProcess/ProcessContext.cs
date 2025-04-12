namespace Not.SystemProcess;

public class ProcessContext
{
    public ProcessContext(string parentPid)
    {
        ParentProcessId = int.Parse(parentPid);
    }

    public int ParentProcessId { get; }
}

namespace Not.ProcessUtility;

public class ProcessServiceContext
{
    public ProcessServiceContext(string parentPID)
    {
        ParentProcessId = int.Parse(parentPID);
    }

    public int ParentProcessId { get; }
}

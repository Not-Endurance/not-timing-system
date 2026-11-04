namespace NC.Startup;

/// <summary>
/// Execute synchronous code at application startup.
/// </summary>
public interface IStartupInitializer
{
    void RunAtStartup();
}

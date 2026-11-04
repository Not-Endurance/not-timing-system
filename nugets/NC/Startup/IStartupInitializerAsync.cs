namespace NC.Startup;

public interface IStartupInitializerAsync
{
    Task RunAtStartupAsync();
}

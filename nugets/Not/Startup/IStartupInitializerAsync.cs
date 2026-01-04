namespace Not.Startup;

public interface IStartupInitializerAsync
{
    Task RunAtStartupAsync();
}

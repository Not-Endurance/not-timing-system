namespace Not.Injection.Config;

public static class NConfigHelper
{
    public static Func<IServiceProvider, T> CreateConfigFactory<T>(Action<T> configure)
        where T : INConfig, new()
    {
        var config = new T();
        configure(config);
        config.Validate();
        return _ => config;
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Application.Configurations;
using Not.Application.HTTP;

namespace Not.Application;

public class NApplicationBuilder
{
    readonly IServiceCollection _services;
    readonly IConfiguration _configuration;

    public NApplicationBuilder(IServiceCollection services, IConfiguration configuration)
    {
        _services = services;
        _configuration = configuration;
    }

    public NApplicationBuilder AddHttp()
    {
        _services.AddHttpClient();
        _services.AddTransient<NHttpClient>();
        _services.AddSettings<NHttpSettings>(_configuration, x => !string.IsNullOrWhiteSpace(x.Host));
        return this;
    }
}

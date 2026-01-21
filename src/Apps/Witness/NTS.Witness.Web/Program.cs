using Not.Application.Configurations;
using Not.Application.CRUD.Ports;
using Not.Startup;
using NTS.Domain.Setup.Aggregates;
using NTS.Witness.Storage;
using NTS.Witness.Storage.Repositories;
using NTS.Witness.Web.Endpoints;
using Serilog;

namespace NTS.Witness.Web;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Configuration.AddNAppsettings(typeof(Program).Assembly);
        builder.Services.ConfigureNtsWitnessWeb(builder.Configuration);
        builder
            .Services.ConfigureWitnessStorage(builder.Configuration, debugRootDirectoryName: "nts-witness")
            .AddRestApiStorage();
        builder.Services.AddSingleton<IRepository<UpcomingEvent>, UpcomingEventRepository>();
        builder.Logging.AddSerilog();

        var app = builder.Build();

        //Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.MapAuthEndpoints();
        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseAntiforgery();
        app.MapBlazorHub();
        app.MapFallbackToPage("/_Host");

        await app.Services.Startup();
        await app.RunAsync();
    }
}

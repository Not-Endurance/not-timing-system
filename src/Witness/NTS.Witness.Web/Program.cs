using Not.Application.Configurations;
using Not.Startup;
using NTS.Witness.Web.Endpoints;
using Serilog;

namespace NTS.Witness.Web;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddRazorPages();
        builder.Services.AddServerSideBlazor();

        var assembly = typeof(Program).Assembly;
        builder.Configuration.AddNAppsettings(assembly);
        builder.Services.ConfigureNtsWitnessWeb(builder.Configuration);

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

        await app.Startup();
    }
}

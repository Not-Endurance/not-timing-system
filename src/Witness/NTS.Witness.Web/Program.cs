using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Not.Application.Configurations;
using Not.Authentication;
using Serilog;

namespace NTS.Witness.Web;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddRazorPages();
        builder.Services.AddServerSideBlazor();

        // Authentication
        //builder
        //    .Services

        //builder.Services.AddAuthorization();

        var assembly = typeof(Program).Assembly;
        builder.Configuration.AddNAppsettings(assembly);
        builder.Services.AddWitnessServices(builder.Configuration);

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

        app.Run();
    }
}

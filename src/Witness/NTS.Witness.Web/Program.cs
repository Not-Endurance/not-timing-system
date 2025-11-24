using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.OAuth;
using Not.Application.Configurations;
using NTS.Witness.Blazor;
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

        // Authentication - extract in external service
        var authConfig = builder.Configuration.GetSection("Auth").Get<AuthConfig>() ?? new AuthConfig();

        var allowedUsersByEmail = authConfig
            .Users.Where(u => !string.IsNullOrWhiteSpace(u.Email))
            .ToDictionary(u => u.Email, StringComparer.OrdinalIgnoreCase);

        builder
            .Services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
            })
            .AddCookie()
            .AddGoogle(options =>
            {
                options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
                options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
                // optional: make sure common claims are populated
                options.ClaimActions.MapJsonKey(ClaimTypes.Email, "email");

                options.Events = new OAuthEvents
                {
                    OnTicketReceived = context =>
                    {
                        var email = context.Principal?.FindFirst(ClaimTypes.Email)?.Value;
                        var oldIdentity = (ClaimsIdentity)context.Principal!.Identity!;

                        var newIdentity = new ClaimsIdentity(
                            oldIdentity.Claims,
                            oldIdentity.AuthenticationType,
                            ClaimTypes.Name,
                            ClaimTypes.Role
                        );

                        // clear old identities and add new one
                        context.Principal = new ClaimsPrincipal(newIdentity);

                        // deny if no email / not gmail / not in allow list
                        if (
                            string.IsNullOrWhiteSpace(email)
                            || !email.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase)
                            || !allowedUsersByEmail.TryGetValue(email, out var authUser)
                        )
                        {
                            context.Response.Redirect("/access-denied");
                            context.Response.StatusCode = 403;
                            context.HandleResponse();
                            return Task.CompletedTask;
                        }

                        foreach (var role in authUser.Roles ?? [])
                        {
                            if (!string.IsNullOrWhiteSpace(role))
                            {
                                newIdentity!.AddClaim(new Claim(ClaimTypes.Role, role));
                            }
                        }

                        return Task.CompletedTask;
                    },
                };
            });

        builder.Services.AddAuthorization();

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

        // Endpoint that triggers Google challenge
        app.MapGet(
            "/signin",
            async (HttpContext ctx) =>
            {
                await ctx.ChallengeAsync(
                    GoogleDefaults.AuthenticationScheme,
                    new AuthenticationProperties { RedirectUri = "/profile" }
                );
            }
        );

        // Endpoint to sign out
        app.MapGet(
            "/signout",
            async (HttpContext ctx) =>
            {
                await ctx.SignOutAsync();
                ctx.Response.Redirect("/profile");
            }
        );
        app.MapGet("/access-denied", () => Results.Text("Access denied."));

        app.UseHttpsRedirection();

        app.UseStaticFiles();
        app.UseAntiforgery();

        app.MapBlazorHub();
        app.MapFallbackToPage("/_Host");

        app.Run();
    }
}

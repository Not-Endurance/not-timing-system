using Not.Identity.Email;
using NoTiming.Api.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddNoTimingApi(builder.Configuration, builder.Environment);

var app = builder.Build();

// The email sender is chosen at start: a sender the host refuses (ADR-0002) stops it here, not at the first code it sends.
_ = app.Services.GetRequiredService<IEmailSender>();
app.UseNoTimingApi();
app.Run();

public partial class Program;

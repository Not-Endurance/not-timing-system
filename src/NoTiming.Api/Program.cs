using NoTiming.Api.Hosting;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddNoTimingApi(builder.Configuration, builder.Environment);

var app = builder.Build();
app.UseNoTimingApi();
app.Run();

public partial class Program;

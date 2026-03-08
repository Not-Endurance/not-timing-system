using NTS.Nexus.Warp;

var builder = Warp.CreateBuilder(args);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    Console.WriteLine(@"******* WARP: Using HTTPS Redirection *******");
    app.UseHttpsRedirection();
}

var port = Environment.GetEnvironmentVariable("PORT"); // Supplied by Azure App service
if (port == null)
{
    throw new ApplicationException("Cannot bind WARP without port. Provide 'PORT' variable");
}
Warp.Start(app, port);


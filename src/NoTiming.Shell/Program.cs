using NoTiming;

var builder = NoTimingHost.CreateBuilder(args);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    Console.WriteLine(@"******* NoTiming: Using HTTPS Redirection *******");
    app.UseHttpsRedirection();
}

var port = Environment.GetEnvironmentVariable("PORT"); // Supplied by Azure App service
if (port == null)
{
    throw new ApplicationException("Cannot bind NoTiming without port. Provide 'PORT' variable");
}
NoTimingHost.Start(app, port);

using NTS.Warp;

var builder = Warp.CreateBuilder(args);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    Console.WriteLine(@"******* WARP: Using HTTPS Redirection *******");  
    app.UseHttpsRedirection();
}

Warp.Start(app);

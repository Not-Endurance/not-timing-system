using NTS.Warp;

var builder = Warp.CreateBuilder(args);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

Warp.Start(app);

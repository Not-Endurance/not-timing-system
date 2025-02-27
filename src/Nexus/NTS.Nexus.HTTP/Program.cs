using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson.Serialization;
using Not.Application.CRUD.Ports;
using NTS.Nexus.HTTP.Functions.Archive;
using NTS.Nexus.HTTP.Functions.Horses;
using NTS.Storage.Documents;
using NTS.Storage.Documents.EnduranceEvents;
using NTS.Storage.Documents.Horses;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services.AddSingleton<IRepository<EnduranceEventDocument>, ArchiveRepository>();
builder.Services.AddSingleton<IArchiveRepository, ArchiveRepository>();
builder.Services.AddSingleton<IRepository<HorseDocument>, HorsesRepository>();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

BsonClassMap.RegisterClassMap<Document>(x => 
{
    x.AutoMap();
    x.MapIdField(x => x.Id);
});

builder.Build().Run();

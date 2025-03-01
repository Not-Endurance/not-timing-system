using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson.Serialization;
using Not.Application.CRUD.Ports;
using Not.Injection;
using NTS.Nexus.HTTP.Functions.Archive;
using NTS.Nexus.HTTP.Functions.Athletes;
using NTS.Nexus.HTTP.Functions.Clubs;
using NTS.Nexus.HTTP.Functions.Countries;
using NTS.Nexus.HTTP.Functions.Horses;
using NTS.Nexus.HTTP.Logger;
using NTS.Nexus.HTTP.Mongo;
using NTS.Storage.Documents;
using NTS.Storage.Documents.Athletes;
using NTS.Storage.Documents.Clubs;
using NTS.Storage.Documents.Countries;
using NTS.Storage.Documents.EnduranceEvents;
using NTS.Storage.Documents.Horses;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services.RegisterConventionalServices();

//builder.Services.AddSingleton<MongoContext>();

//builder.Services.AddTransient<IRepository<EnduranceEventDocument>, ArchiveRepository>();
//builder.Services.AddTransient<IArchiveRepository, ArchiveRepository>();
//builder.Services.AddTransient<IRepository<HorseDocument>, HorseRepository>();
//builder.Services.AddTransient<IRepository<AthleteDocument>, AthleteRepository>();
//builder.Services.AddTransient<IRepository<CountryDocument>, CountryRepository>();
//builder.Services.AddTransient<IRepository<ClubDocument>, ClubRepository>();

builder.Services.AddTransient(typeof(IFunctionLogger<>), typeof(FunctionLogger<>));

builder
    .Services.AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

BsonClassMap.RegisterClassMap<Document>(x =>
{
    x.AutoMap();
    x.MapIdField(x => x.Id);
});

builder.Build().Run();

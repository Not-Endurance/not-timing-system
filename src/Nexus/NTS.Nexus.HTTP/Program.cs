using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson.Serialization;
using Not.Injection;
using NTS.Nexus.HTTP.Mongo;
using NTS.Storage.Documents;

var connectionString = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING");

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services.AddMongo(connectionString);
builder.Services.RegisterConventionalServices();

builder.Services.AddApplicationInsightsTelemetryWorkerService().ConfigureFunctionsApplicationInsights();

BsonClassMap.RegisterClassMap<Document>(x =>
{
    x.AutoMap();
    x.MapIdField(x => x.Id);
});

builder.Build().Run();

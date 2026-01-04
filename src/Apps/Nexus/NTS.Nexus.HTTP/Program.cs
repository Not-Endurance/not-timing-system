using System;
using System.Reflection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Not.Injection;
using NTS.Application.Models;
using NTS.Nexus.HTTP.Mongo;

var connectionString = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING");

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services.AddMongo(connectionString);
builder.Services.AddNConventionalServices(Assembly.GetExecutingAssembly());

builder.Services.AddApplicationInsightsTelemetryWorkerService().ConfigureFunctionsApplicationInsights();

BsonClassMap.RegisterClassMap<Document>(x =>
{
    x.AutoMap();
    x.MapIdField(x => x.Id);
});
BsonSerializer.RegisterSerializer(typeof(DateTimeOffset), new DateTimeOffsetSerializer(BsonType.DateTime));

builder.Build().Run();

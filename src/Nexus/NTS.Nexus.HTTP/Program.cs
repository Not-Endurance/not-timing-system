using System;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using Not.Injection;
using NTS.Application.DataTransferObjects;
using NTS.Nexus.HTTP.Mongo;

var connectionString = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING");

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services.AddMongo(connectionString);
builder.Services.RegisterConventionalServices();

builder.Services.AddApplicationInsightsTelemetryWorkerService().ConfigureFunctionsApplicationInsights();

BsonClassMap.RegisterClassMap<Identity>(x =>
{
    x.AutoMap();
    x.MapIdField(x => x.Id);
});
BsonSerializer.RegisterSerializer(typeof(DateTimeOffset), new DateTimeOffsetSerializer(BsonType.DateTime));

builder.Build().Run();

#!/usr/bin/env dotnet

#:sdk Microsoft.NET.Sdk.Web
#:package Microsoft.AspNetCore.OpenApi@11.0.0-rc.1.26425.128

using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/{name?}", (string name = "World") => new HelloResponse { Message = $"Hello, {name}!" })
    .WithName("HelloWorld");

app.Run();

public class HelloResponse
{
    public required string Message { get; set; }
}

[JsonSerializable(typeof(HelloResponse))]
partial class AppJsonSerializerContext : JsonSerializerContext
{

}

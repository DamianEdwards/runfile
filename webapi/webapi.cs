#!/usr/bin/env cs

#:sdk Microsoft.NET.Sdk.Web
#:package Microsoft.AspNetCore.OpenApi 10.0.0-*

var builder = WebApplication.CreateBuilder(args);

var settingsFile = $"{builder.Environment.ApplicationName}.appsettings.json";
builder.Configuration.AddJsonFile(settingsFile, optional: true, reloadOnChange: true);

builder.Services.AddOpenApi();

var app = builder.Build();

app.MapGet("/", () => new { Message = "Hello, World!" })
    .WithName("HelloWorld");

app.Run();

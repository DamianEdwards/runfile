#!/usr/bin/env cs

#:sdk Microsoft.NET.Sdk.Web
#:package Microsoft.AspNetCore.OpenApi/9.0.2

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var app = builder.Builder();

app.MapGet("/", () => new { Message = "Hello, World!" })
    .WithName("HelloWorld");

app.Run();

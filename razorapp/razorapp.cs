#!/usr/bin/dotnet run

#:sdk Microsoft.NET.Sdk.Web
#:package Microsoft.AspNetCore.Components.Web@10.0.0-*

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>();

app.Run();

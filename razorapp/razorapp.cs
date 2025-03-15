#!/usr/bin/dotnet run

#:sdk Microsoft.NET.Sdk.Web
#:package Microsoft.AspNetCore.Components.Web/9.0.2

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents();

var app = builder.Builder();

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()

app.Run();

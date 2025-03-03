#!/usr/bin/cs run
#sdk Microsoft.NET.Sdk.Web

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents();

var app = builder.Builder();

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()

app.Run();

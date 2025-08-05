#!/usr/bin/env dotnet

#:sdk Microsoft.NET.Sdk.Web
#:property BlazorDisableThrowNavigationException=true
#:property PublishAot=false

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<helloblazor.pages.Index>();

app.Run();

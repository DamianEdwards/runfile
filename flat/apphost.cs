#!/usr/bin/env aspire

#:sdk Aspire.AppHost.Sdk@13.3.3
//#:package Aspire.Hosting.Redis@13.3.3

var builder = DistributedApplication.CreateBuilder(args);

var webapi = builder.AddCSharpApp("webapi", "./webapi.cs");

builder.AddCSharpApp("razorapp", "../razorapp/razorapp.cs")
    .WithReference(webapi).WaitFor(webapi);

// if (!string.Equals(builder.Configuration["DOTNET_LAUNCH_PROFILE"], "verify", StringComparison.OrdinalIgnoreCase))
// {
//     var redis = builder.AddRedis("redis");
//     webapi.WithReference(redis).WaitFor(redis);
// }

builder.Build().Run();
#!/usr/bin/env dotnet

#:sdk Microsoft.NET.Sdk
#:sdk Aspire.AppHost.Sdk 9.3.0
#:package Aspire.Hosting.AppHost@9.3.0
#:package Aspire.Hosting.Redis@9.3.0

var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("redis");

// builder.AddDotnetApp("../webapi/webapi.cs")
//     .WithReference(redis).WaitFor(redis);

builder.Build().Run();

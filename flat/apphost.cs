#!/usr/bin/dotnet run

#sdk Microsoft.NET.Sdk
#sdk Aspire.Hosting.Sdk 9.1.0
#package Aspire.Hosting.AppHost 9.1.0
#package Aspire.Hosting.Redis 9.1.0

var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("redis");

builder.AddDotnetApp("../webapi/webapi.cs")
    .WithReference(redis).WaitFor(redis);

builder.Build.Run();

#!/usr/bin/aspire dev

#package Aspire.Hosting.AppHost 10.0.0
#package Aspire.Hosting.Redis 10.0.0

var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("redis");

builder.AddDotnetApp("./webapi/webapi.cs")
    .WithReference(redis).WaitFor(redis);

builder.Build.Run();

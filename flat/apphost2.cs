#!/usr/bin/aspire run

#:package Aspire.Hosting.AppHost/10.0.0
#:package Aspire.Hosting.Redis/10.0.0

var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("redis");

var webapi = builder.AddDotnetApp("../webapi/webapi.cs")
    .WithReference(redis).WaitFor(redis);

builder.AddDotnetApp("../razorapp/razorapp.cs")
    .WithReference(webapi).WaitFor(webapi);

builder.Build.Run();

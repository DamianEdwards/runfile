#!/usr/bin/dotnet run
#:package Microsoft.NET.Test.Sdk@17.*
#:package xunit.v3@2.*
#:package xunit.runner.visualstudio@3.*
#:property TestingPlatformDotnetTestSupport true
#:property UseMicrosoftTestingPlatformRunner true
#:property OutputType Exe

using Xunit;

public class UnitTest1
{
    public UnitTest1()
    {
        // This constructor runs before each test.
        // You can add setup logic here if needed.
        Console.WriteLine("Constructor: This runs before each test.");
    }

    [Fact]
    public void Test1()
    {
        Assert.True(true);
    }
}

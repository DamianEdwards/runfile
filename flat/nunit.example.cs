#!/usr/bin/dotnet run
#:package coverlet.collector@3.*
#:package Microsoft.NET.Test.Sdk@17.*
#:package NUnit@4.*
#:package NUnit.Analyzers@4.*
#:package NUnit3TestAdapter@5.*
#:property EnableNUnitRunner true
#:property TestingPlatformDotnetTestSupport true

using NUnit.Framework;

public class Tests
{
    [SetUp]
    public void Setup()
    {
        Console.WriteLine("Setup: This method runs before each test.");
    }

    [Test]
    public void Test1()
    {
        Assert.Pass();
    }
}

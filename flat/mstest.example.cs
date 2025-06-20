#!/usr/bin/dotnet run
#:package Microsoft.NET.Test.Sdk@17.*
#:package MSTest@3.*
#:property EnableMSTestRunner true
#:property TestingPlatformDotnetTestSupport true
#:property OutputType exe 
using Microsoft.VisualStudio.TestTools.UnitTesting;
[TestClass]
public sealed class Test1
{
    [TestMethod]
    public void TestMethod1()
    {
        // This is a placeholder for a test method.
        // You can add your test logic here.
        Assert.IsTrue(true, "This test always passes.");
    }
}

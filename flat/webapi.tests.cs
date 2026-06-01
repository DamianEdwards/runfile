#!/usr/bin/env dotnet

#:package xunit.v3@3.2.2
#:package Microsoft.AspNetCore.Mvc.Testing@10.0.8
#:property PublishAot=false

// Reference the webapi project to test it
#:ref ./webapi.cs
#:property ExperimentalFileBasedProgramEnableRefDirective=true

using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

public class BasicTests : IClassFixture<WebApplicationFactory<HelloResponse>>
{
    private readonly WebApplicationFactory<HelloResponse> _factory;

    public BasicTests(WebApplicationFactory<HelloResponse> factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/test")]
    public async Task GetHelloApi_ReturnSuccessAndCorrectContentType(string url)
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(url, TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode(); // Status Code 200-299
        Assert.Equal("application/json; charset=utf-8", response.Content.Headers.ContentType?.ToString());
    }

    [Fact]
    public async Task GetHelloApi_WithNull_ReturnsDefaultMessage()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/", TestContext.Current.CancellationToken);

        var helloResponse = await response.Content.ReadFromJsonAsync<HelloResponse>(TestContext.Current.CancellationToken);
        Assert.Equal("Hello, World!", helloResponse?.Message);
    }

    [Theory]
    [InlineData("World")]
    [InlineData("test")]
    public async Task GetHelloApi_ReturnsExpectedMessage(string name)
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/{name}", TestContext.Current.CancellationToken);

        var helloResponse = await response.Content.ReadFromJsonAsync<HelloResponse>(TestContext.Current.CancellationToken);
        Assert.Equal($"Hello, {name}!", helloResponse?.Message);
    }
}
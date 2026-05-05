using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace Runfile.SweBench.Tests;

public sealed class LaunchProfileTests
{
    [Fact]
    public async Task VerifyLaunchProfileFlowsIntoDiagnosticsEndpoint()
    {
        await using var app = await FileAppServer.StartAsync(Path.Combine("flat", "webapi.cs"), launchProfile: "verify");

        var json = await app.Client.GetFromJsonAsync<JsonObject>("/diagnostics/runtime");

        Assert.NotNull(json);
        Assert.Equal("Development", json["environment"]?.GetValue<string>());
        Assert.Equal("verify", json["launchProfile"]?.GetValue<string>());
        Assert.Equal("Hello from the verify launch profile", json["greeting"]?.GetValue<string>());
        Assert.NotNull(json["urls"]?.AsArray());
    }

    [Fact]
    public async Task GreetingCanComeFromEnvironmentWhenNoLaunchProfileIsSelected()
    {
        await using var app = await FileAppServer.StartAsync(
            Path.Combine("flat", "webapi.cs"),
            environment: new Dictionary<string, string?> { ["RUNFILE_GREETING"] = "Hello from env" });

        var json = await app.Client.GetFromJsonAsync<JsonObject>("/diagnostics/runtime");

        Assert.NotNull(json);
        Assert.Equal("Hello from env", json["greeting"]?.GetValue<string>());
    }
}

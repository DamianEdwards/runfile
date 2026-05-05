using Xunit;

namespace Runfile.SweBench.Tests;

public sealed class DisableAotJsonPublishTests : IDisposable
{
    private readonly string _publishPath = Path.Combine(Path.GetTempPath(), $"runfile-noaot-json-{Guid.NewGuid():N}");
    private readonly string _sampleJsonPath = Path.Combine(Path.GetTempPath(), $"settings-{Guid.NewGuid():N}.json");

    [Fact]
    public void JsonInspectorExplicitlyDisablesNativeAot()
    {
        var sourcePath = RepoPaths.Combine("flat", "json-inspect.cs");
        Assert.True(File.Exists(sourcePath), "Expected flat/json-inspect.cs.");

        var source = File.ReadAllText(sourcePath);
        Assert.Contains("#:property PublishAot=false", source);
        Assert.Contains("JsonNode", source);
    }

    [Fact]
    public async Task JsonInspectorPublishesToCustomOutputAndRuns()
    {
        Directory.CreateDirectory(_publishPath);
        File.WriteAllText(_sampleJsonPath, """
            {
              "serviceName": "orders-api",
              "replicas": 3
            }
            """);

        var publish = await Dotnet.RunAsync(
            ["publish", Path.Combine("flat", "json-inspect.cs"), "--output", _publishPath],
            timeoutSeconds: 300);

        Assert.Equal(0, publish.ExitCode);

        var run = await PublishedApp.RunAsync(_publishPath, "json-inspect", _sampleJsonPath, "serviceName");

        Assert.Equal(0, run.ExitCode);
        Assert.Equal("orders-api", run.Stdout.Trim().Trim('"'));
    }

    public void Dispose()
    {
        if (Directory.Exists(_publishPath))
        {
            Directory.Delete(_publishPath, recursive: true);
        }

        if (File.Exists(_sampleJsonPath))
        {
            File.Delete(_sampleJsonPath);
        }
    }
}

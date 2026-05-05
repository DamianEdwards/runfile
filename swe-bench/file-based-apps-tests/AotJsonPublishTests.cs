using Xunit;

namespace Runfile.SweBench.Tests;

public sealed class AotJsonPublishTests : IDisposable
{
    private readonly string _publishPath = Path.Combine(Path.GetTempPath(), $"runfile-aot-json-{Guid.NewGuid():N}");
    private readonly string _sampleJsonPath = Path.Combine(Path.GetTempPath(), $"orders-{Guid.NewGuid():N}.json");

    [Fact]
    public void JsonSummaryUsesSourceGeneratedJsonAndKeepsNativeAotEnabled()
    {
        var sourcePath = RepoPaths.Combine("flat", "json-summary.cs");
        Assert.True(File.Exists(sourcePath), "Expected flat/json-summary.cs.");

        var source = File.ReadAllText(sourcePath);
        Assert.Contains("JsonSerializerContext", source);
        Assert.Contains("JsonSerializable", source);
        Assert.DoesNotContain("PublishAot=false", source);
    }

    [Fact]
    public async Task JsonSummaryPublishesToCustomOutputAndRuns()
    {
        Directory.CreateDirectory(_publishPath);
        File.WriteAllText(_sampleJsonPath, """
            [
              { "id": 1, "customer": "Ada", "total": 12.50 },
              { "id": 2, "customer": "Grace", "total": 7.25 }
            ]
            """);

        var publish = await Dotnet.RunAsync(
            ["publish", Path.Combine("flat", "json-summary.cs"), "--output", _publishPath],
            timeoutSeconds: 300);

        Assert.Equal(0, publish.ExitCode);

        var run = await PublishedApp.RunAsync(_publishPath, "json-summary", _sampleJsonPath);

        Assert.Equal(0, run.ExitCode);
        Assert.Contains("Orders: 2", run.Stdout);
        Assert.Contains("Total: 19.75", run.Stdout);
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

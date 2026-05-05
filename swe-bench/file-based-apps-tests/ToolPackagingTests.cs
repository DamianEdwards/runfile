using Xunit;

namespace Runfile.SweBench.Tests;

public sealed class ToolPackagingTests : IDisposable
{
    private readonly string _outputPath = Path.Combine(Path.GetTempPath(), $"runfile-pack-{Guid.NewGuid():N}");

    [Fact]
    public async Task GreetFileAppPacksAsToolWithExpectedPackageIdentity()
    {
        Directory.CreateDirectory(_outputPath);

        var source = File.ReadAllText(RepoPaths.Combine("flat", "greet.cs"));
        Assert.Contains("PackageId=Runfile.Greet", source);
        Assert.Contains("Version=1.0.0", source);
        Assert.Contains("ToolCommandName=greet", source);

        var result = await Dotnet.RunAsync("pack", Path.Combine("flat", "greet.cs"), "--output", _outputPath);

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(Path.Combine(_outputPath, "Runfile.Greet.1.0.0.nupkg")), result.Stdout + result.Stderr);
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputPath))
        {
            Directory.Delete(_outputPath, recursive: true);
        }
    }
}

using Xunit;

namespace Runfile.SweBench.Tests;

public sealed class BuildOutputTests : IDisposable
{
    private readonly string _outputPath = RepoPaths.Combine("flat", "artifacts", "report-build");

    [Fact]
    public async Task ReportAppRunsAndBuildsToConfiguredOutputPath()
    {
        var sourcePath = RepoPaths.Combine("flat", "report.cs");
        Assert.True(File.Exists(sourcePath), "Expected flat/report.cs.");

        var source = File.ReadAllText(sourcePath);
        Assert.Contains("#:property OutputPath=./artifacts/report-build", source);

        var runResult = await Dotnet.RunAsync("run", "--file", Path.Combine("flat", "report.cs"));
        Assert.Equal(0, runResult.ExitCode);
        Assert.Equal("report ready", runResult.Stdout.Trim());

        if (Directory.Exists(_outputPath))
        {
            Directory.Delete(_outputPath, recursive: true);
        }

        var buildResult = await Dotnet.RunAsync("build", Path.Combine("flat", "report.cs"));
        Assert.Equal(0, buildResult.ExitCode);
        Assert.True(Directory.Exists(_outputPath), buildResult.Stdout + buildResult.Stderr);
        Assert.NotEmpty(Directory.EnumerateFileSystemEntries(_outputPath, "*", SearchOption.AllDirectories));
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputPath))
        {
            Directory.Delete(_outputPath, recursive: true);
        }
    }
}

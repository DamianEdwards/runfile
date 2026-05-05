using Xunit;

namespace Runfile.SweBench.Tests;

public sealed class ConvertProjectTests
{
    [Fact]
    public void ConversionKeepsOriginalFileAppAndCreatesProject()
    {
        Assert.True(File.Exists(RepoPaths.Combine("flat", "greet.cs")), "Original flat/greet.cs should remain.");
        Assert.True(Directory.Exists(RepoPaths.Combine("flat", "greet")), "Expected converted project folder flat/greet.");
        Assert.True(File.Exists(RepoPaths.Combine("flat", "greet", "greet.csproj")), "Expected converted project file flat/greet/greet.csproj.");
    }

    [Fact]
    public async Task ConvertedProjectStillGreetsNames()
    {
        var projectPath = Path.Combine("flat", "greet", "greet.csproj");
        var result = await Dotnet.RunAsync("run", "--project", projectPath, "--", "Ada");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Hello, Ada!", result.Stdout);
    }
}

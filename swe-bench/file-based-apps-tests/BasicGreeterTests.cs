using Xunit;

namespace Runfile.SweBench.Tests;

public sealed class BasicGreeterTests
{
    private static readonly string GreetApp = Path.Combine("flat", "greet.cs");

    [Fact]
    public void GreetAppExistsAsSingleCSharpFile()
    {
        Assert.True(File.Exists(RepoPaths.Combine("flat", "greet.cs")), "Expected flat/greet.cs to exist.");
        Assert.False(File.Exists(RepoPaths.Combine("flat", "greet.csproj")), "The tiny greet app should not need a project file next to it.");
        Assert.False(Directory.Exists(RepoPaths.Combine("flat", "greet")), "The tiny greet app should not be moved into a project folder.");
    }

    [Fact]
    public async Task RunWithNoArgumentsGreetsWorld()
    {
        var result = await Dotnet.RunAsync("run", "--file", GreetApp);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("Hello, World!", result.Stdout.Trim());
    }

    [Fact]
    public async Task RunWithNameGreetsThatName()
    {
        var result = await Dotnet.RunAsync("run", "--file", GreetApp, "--", "Ada");

        Assert.Equal(0, result.ExitCode);
        Assert.Equal("Hello, Ada!", result.Stdout.Trim());
    }
}

using Xunit;

namespace Runfile.SweBench.Tests;

public sealed class CatCliFeatureTests
{
    private static readonly string CatApp = Path.Combine("flat", "cat.cs");

    [Fact]
    public async Task NumberAndSqueezeBlankCanBeCombined()
    {
        using var temp = new TempTextFile("""
            alpha


            beta
            gamma
            """);

        var result = await Dotnet.RunAsync("run", "--file", CatApp, "--", temp.Path, "--number", "--squeeze-blank");

        Assert.Equal(0, result.ExitCode);
        var output = Normalize(result.Stdout);
        Assert.Contains("     1\talpha", output);
        Assert.Contains("     2\t", output);
        Assert.Contains("     3\tbeta", output);
        Assert.Contains("     4\tgamma", output);
        Assert.DoesNotContain("\n\n\n", output);
    }

    [Fact]
    public async Task FindWithIgnoreCasePrintsOnlyMatchingLines()
    {
        using var temp = new TempTextFile("""
            alpha
            Beta
            alphabet
            gamma
            """);

        var result = await Dotnet.RunAsync("run", "--file", CatApp, "--", temp.Path, "--find", "ALP", "--ignore-case");

        Assert.Equal(0, result.ExitCode);
        var output = Normalize(result.Stdout);
        Assert.Contains("alpha", output);
        Assert.Contains("alphabet", output);
        Assert.DoesNotContain("Beta", output);
        Assert.DoesNotContain("gamma", output);
    }

    [Fact]
    public async Task FindWithNoMatchesReturnsOneAndWritesStderr()
    {
        using var temp = new TempTextFile("""
            alpha
            beta
            """);

        var result = await Dotnet.RunAsync("run", "--file", CatApp, "--", temp.Path, "--find", "does-not-exist");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("No matches found.", result.Stderr);
        Assert.DoesNotContain("alpha", result.Stdout);
        Assert.DoesNotContain("beta", result.Stdout);
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n");
}

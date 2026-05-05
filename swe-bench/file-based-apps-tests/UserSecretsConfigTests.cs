using Xunit;

namespace Runfile.SweBench.Tests;

public sealed class UserSecretsConfigTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), $"runfile-secrets-{Guid.NewGuid():N}");
    private readonly string _appFile;

    public UserSecretsConfigTests()
    {
        Directory.CreateDirectory(_tempDir);
        _appFile = Path.Combine(_tempDir, "configtool.cs");
        File.Copy(RepoPaths.Combine("flat", "usersecrets.cs"), _appFile);
    }

    [Fact]
    public async Task UserSecretIsFoundAndRedactedByDefault()
    {
        var key = $"Bench:{Guid.NewGuid():N}";
        await RunExpectedAsync(["user-secrets", "set", key, "from-secret", "--file", _appFile], expectedExitCode: 0);

        var result = await RunExpectedAsync(["run", "--file", _appFile, "--", key], expectedExitCode: 0);

        Assert.Contains($"{key}: found", result.Stdout);
        Assert.Contains("user secrets", result.Stdout, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("from-secret", result.Stdout);
    }

    [Fact]
    public async Task ShowValuesPrintsSecretValue()
    {
        var key = $"Bench:{Guid.NewGuid():N}";
        await RunExpectedAsync(["user-secrets", "set", key, "from-secret", "--file", _appFile], expectedExitCode: 0);

        var result = await RunExpectedAsync(["run", "--file", _appFile, "--", key, "--show-values"], expectedExitCode: 0);

        Assert.Contains($"{key}=from-secret", result.Stdout);
    }

    [Fact]
    public async Task EnvironmentVariablesUseDoubleUnderscoreNesting()
    {
        var key = $"Bench:{Guid.NewGuid():N}:EnvOnly";
        var envName = key.Replace(":", "__");

        var result = await RunExpectedAsync(
            ["run", "--file", _appFile, "--", key, "--show-values"],
            expectedExitCode: 0,
            environment: new Dictionary<string, string?> { [envName] = "from-env" });

        Assert.Contains($"{key}=from-env", result.Stdout);
        Assert.Contains("environment", result.Stdout, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CommandLineSetOverridesEnvironmentAndUserSecrets()
    {
        var key = $"Bench:{Guid.NewGuid():N}:ApiKey";
        await RunExpectedAsync(["user-secrets", "set", key, "from-secret", "--file", _appFile], expectedExitCode: 0);
        var envName = key.Replace(":", "__");

        var result = await RunExpectedAsync(
            ["run", "--file", _appFile, "--", key, "--set", $"{key}=from-cli", "--show-values"],
            expectedExitCode: 0,
            environment: new Dictionary<string, string?> { [envName] = "from-env" });

        Assert.Contains($"{key}=from-cli", result.Stdout);
        Assert.Contains("command line", result.Stdout, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("from-env", result.Stdout);
        Assert.DoesNotContain("from-secret", result.Stdout);
    }

    [Fact]
    public async Task MissingKeyReturnsTwoAfterReportingAllKeys()
    {
        var existing = $"Bench:{Guid.NewGuid():N}:Existing";
        var missing = $"Bench:{Guid.NewGuid():N}:Missing";
        await RunExpectedAsync(["user-secrets", "set", existing, "from-secret", "--file", _appFile], expectedExitCode: 0);

        var result = await RunExpectedAsync(["run", "--file", _appFile, "--", existing, missing], expectedExitCode: 2);

        Assert.Contains($"{existing}: found", result.Stdout);
        Assert.Contains($"{missing}: missing", result.Stdout);
    }

    [Fact]
    public async Task NoKeysReturnsUsageError()
    {
        var result = await RunExpectedAsync(["run", "--file", _appFile], expectedExitCode: 64);

        Assert.Contains("Usage:", result.Stderr);
    }

    public void Dispose()
    {
        Directory.Delete(_tempDir, recursive: true);
    }

    private static async Task<ProcessResult> RunExpectedAsync(
        string[] args,
        int expectedExitCode,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        var result = await Dotnet.RunAsync(args, environment);

        Assert.True(result.ExitCode == expectedExitCode, $"""
            Expected exit code {expectedExitCode}, got {result.ExitCode}.
            Stdout:
            {result.Stdout}
            Stderr:
            {result.Stderr}
            """);

        return result;
    }
}

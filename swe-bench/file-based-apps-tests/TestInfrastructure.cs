using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;

namespace Runfile.SweBench.Tests;

internal sealed record ProcessResult(int ExitCode, string Stdout, string Stderr);

internal static class RepoPaths
{
    public static string Root { get; } = FindScenarioRoot();

    public static string Combine(params string[] parts)
    {
        var allParts = new string[parts.Length + 1];
        allParts[0] = Root;
        Array.Copy(parts, 0, allParts, 1, parts.Length);
        return Path.Combine(allParts);
    }

    private static string FindScenarioRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable("RUNFILE_SCENARIO_ROOT");
        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            var fullPath = Path.GetFullPath(configuredRoot);
            if (!Directory.Exists(fullPath))
            {
                throw new DirectoryNotFoundException($"RUNFILE_SCENARIO_ROOT does not exist: {fullPath}");
            }

            return fullPath;
        }

        return FindDefaultStarterRoot();
    }

    private static string FindDefaultStarterRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var starterRoot = Path.Combine(directory.FullName, "swe-bench", "file-based-apps-starters", "base");
            if (File.Exists(Path.Combine(starterRoot, "global.json")))
            {
                return starterRoot;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find the default file-based app starter root. Set RUNFILE_SCENARIO_ROOT to the scenario workspace path.");
    }
}

internal static class Dotnet
{
    public static Task<ProcessResult> RunAsync(params string[] args)
        => RunAsync(args, environment: null);

    public static async Task<ProcessResult> RunAsync(
        IReadOnlyList<string> args,
        IReadOnlyDictionary<string, string?>? environment = null,
        int timeoutSeconds = 120)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = RepoPaths.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        if (environment is not null)
        {
            foreach (var (key, value) in environment)
            {
                process.StartInfo.Environment[key] = value;
            }
        }

        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw new TimeoutException($"dotnet {string.Join(' ', args)} timed out.");
        }

        return new ProcessResult(process.ExitCode, await stdoutTask, await stderrTask);
    }
}

internal static class PublishedApp
{
    public static async Task<ProcessResult> RunAsync(string outputDirectory, string appName, params string[] args)
    {
        var appPath = FindPublishedApp(outputDirectory, appName);
        if (Path.GetExtension(appPath).Equals(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return await Dotnet.RunAsync(["exec", appPath, .. args], timeoutSeconds: 120);
        }

        return await RunProcessAsync(appPath, args, outputDirectory);
    }

    private static string FindPublishedApp(string outputDirectory, string appName)
    {
        var candidates = OperatingSystem.IsWindows()
            ? new[] { $"{appName}.exe", $"{appName}.dll" }
            : new[] { appName, $"{appName}.dll" };

        foreach (var candidate in candidates)
        {
            var path = Path.Combine(outputDirectory, candidate);
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new FileNotFoundException($"Could not find published app '{appName}' in {outputDirectory}.");
    }

    private static async Task<ProcessResult> RunProcessAsync(string fileName, IReadOnlyList<string> args, string workingDirectory)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        process.Start();
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw new TimeoutException($"{fileName} {string.Join(' ', args)} timed out.");
        }

        return new ProcessResult(process.ExitCode, await stdoutTask, await stderrTask);
    }
}

internal sealed class TempTextFile : IDisposable
{
    public TempTextFile(string contents)
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
        File.WriteAllText(Path, Normalize(contents.TrimStart()));
    }

    public string Path { get; }

    public void Dispose()
    {
        File.Delete(Path);
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n");
}

internal sealed class FileAppServer : IAsyncDisposable
{
    private readonly Process _process;
    private readonly List<string> _output = [];
    private readonly List<string> _error = [];

    private FileAppServer(Process process, Uri baseAddress)
    {
        _process = process;
        Client = new HttpClient { BaseAddress = baseAddress };
    }

    public HttpClient Client { get; }

    public static async Task<FileAppServer> StartAsync(
        string appRelativePath,
        string? launchProfile = null,
        IReadOnlyDictionary<string, string?>? environment = null)
    {
        var port = GetFreePort();
        var baseAddress = new Uri($"http://127.0.0.1:{port}");

        var args = new List<string> { "run", "--file", appRelativePath };
        if (!string.IsNullOrWhiteSpace(launchProfile))
        {
            args.Add("--launch-profile");
            args.Add(launchProfile);
        }

        args.Add("--");
        args.Add("--urls");
        args.Add(baseAddress.ToString());

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = RepoPaths.Root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            },
            EnableRaisingEvents = true
        };

        foreach (var arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        process.StartInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        process.StartInfo.Environment["DOTNET_ENVIRONMENT"] = "Development";

        if (environment is not null)
        {
            foreach (var (key, value) in environment)
            {
                process.StartInfo.Environment[key] = value;
            }
        }

        var server = new FileAppServer(process, baseAddress);

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                server._output.Add(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                server._error.Add(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await server.WaitUntilReadyAsync();
        return server;
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        if (!_process.HasExited)
        {
            _process.Kill(entireProcessTree: true);
            await _process.WaitForExitAsync();
        }

        _process.Dispose();
    }

    private async Task WaitUntilReadyAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        while (!cts.IsCancellationRequested)
        {
            if (_process.HasExited)
            {
                throw new InvalidOperationException($"""
                    File app exited before it became ready.
                    Stdout:
                    {string.Join(Environment.NewLine, _output)}
                    Stderr:
                    {string.Join(Environment.NewLine, _error)}
                    """);
            }

            try
            {
                using var response = await Client.GetAsync("/", cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException) when (!cts.IsCancellationRequested)
            {
            }

            await Task.Delay(250, cts.Token);
        }

        throw new TimeoutException($"""
            Timed out waiting for file app readiness.
            Stdout:
            {string.Join(Environment.NewLine, _output)}
            Stderr:
            {string.Join(Environment.NewLine, _error)}
            """);
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

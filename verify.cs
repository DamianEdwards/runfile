#!/bin/usr/env dotnet
#:package Spectre.Console@0.54.0
#:property AllowUnsafeBlocks=true

using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using Spectre.Console;

// Parse command-line arguments
var useParallel = args.Contains("--parallel", StringComparer.OrdinalIgnoreCase);
var timeoutSeconds = 10; // Default timeout
for (int i = 0; i < args.Length; i++)
{
    if (args[i].Equals("--timeout", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
    {
        if (int.TryParse(args[i + 1], out var parsed))
        {
            timeoutSeconds = parsed;
        }
    }
}

// Get the path of this script file
var scriptPath = AppContext.GetData("EntryPointFilePath")?.ToString();
if (string.IsNullOrEmpty(scriptPath))
{
    AnsiConsole.MarkupLine("[red]Error: Could not determine script path[/]");
    return 1;
}

var scriptDir = Path.GetDirectoryName(scriptPath) ?? Environment.CurrentDirectory;
var ignoredPathCache = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
AnsiConsole.MarkupLine($"[cyan]Scanning for .cs files from:[/] {scriptDir}");
AnsiConsole.MarkupLine($"[cyan]Execution mode:[/] {(useParallel ? "Parallel" : "Sequential")}");
AnsiConsole.MarkupLine($"[cyan]Timeout:[/] {timeoutSeconds}s");
AnsiConsole.WriteLine();

// Find all .cs files in subdirectories, excluding files that are included by other file-based apps.
var includedFiles = FindIncludedCsFiles(scriptDir);
var csFiles = FindExecutableCsFiles(scriptDir, includedFiles);

if (csFiles.Count == 0)
{
    AnsiConsole.MarkupLine("[yellow]No executable .cs files found[/]");
    return 0;
}

if (includedFiles.Count > 0)
{
    AnsiConsole.MarkupLine($"[cyan]Excluding {includedFiles.Count} included .cs file(s)[/]");
}

AnsiConsole.MarkupLine($"[green]Found {csFiles.Count} .cs file(s) to verify[/]");
AnsiConsole.WriteLine();

// Run verification
var results = useParallel 
    ? await VerifyFilesParallel(csFiles, timeoutSeconds)
    : await VerifyFilesSequential(csFiles, timeoutSeconds);

// Display results in a table
DisplayResults(results);

// Return exit code based on results
var failedCount = results.Count(r => !r.Success);
return failedCount > 0 ? 1 : 0;

// --- Helper Methods ---

bool HasVerifyLaunchProfile(string csFilePath)
{
    try
    {
        var directory = Path.GetDirectoryName(csFilePath);
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(csFilePath);
        var runJsonPath = Path.Combine(directory ?? ".", $"{fileNameWithoutExtension}.run.json");
        
        if (!File.Exists(runJsonPath))
        {
            return false;
        }
        
        var jsonContent = File.ReadAllText(runJsonPath);
        using var doc = JsonDocument.Parse(jsonContent, new JsonDocumentOptions { AllowTrailingCommas = true });
        
        if (doc.RootElement.TryGetProperty("profiles", out var profiles))
        {
            return profiles.TryGetProperty("verify", out _);
        }
    }
    catch
    {
        // If we can't read or parse the file, just proceed without the launch profile
    }
    
    return false;
}

bool ShouldSkipFile(string csFilePath)
{
    try
    {
        // Read the file header (up to first code line)
        foreach (var line in File.ReadLines(csFilePath))
        {
            var trimmed = line.Trim();
            
            // Stop at first code line (non-comment, non-directive, non-blank)
            if (!string.IsNullOrWhiteSpace(trimmed) && 
                !trimmed.StartsWith("#") && 
                !trimmed.StartsWith("//") && 
                !trimmed.StartsWith("/*"))
            {
                break;
            }
            
            // Check for TargetFramework property directive
            if (trimmed.StartsWith("#:property TargetFramework=", StringComparison.OrdinalIgnoreCase))
            {
                var tfm = trimmed.Substring("#:property TargetFramework=".Length).Trim();
                
                // Check if TFM is OS-specific and doesn't match current OS
                if (tfm.Contains("-windows", StringComparison.OrdinalIgnoreCase) && !OperatingSystem.IsWindows())
                {
                    return true;
                }
                if (tfm.Contains("-linux", StringComparison.OrdinalIgnoreCase) && !OperatingSystem.IsLinux())
                {
                    return true;
                }
                if (tfm.Contains("-macos", StringComparison.OrdinalIgnoreCase) && !OperatingSystem.IsMacOS())
                {
                    return true;
                }
            }
        }
    }
    catch
    {
        // If we can't read the file, don't skip it
    }
    
    return false;
}

HashSet<string> FindIncludedCsFiles(string rootDir)
{
    var includedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var file in EnumerateCandidateCsFiles(rootDir))
    {
        foreach (var includePath in ReadIncludeDirectives(file))
        {
            foreach (var resolvedPath in ResolveIncludePath(file, includePath))
            {
                includedFiles.Add(resolvedPath);
            }
        }
    }

    return includedFiles;
}

IEnumerable<string> EnumerateCandidateCsFiles(string rootDir)
{
    foreach (var dir in Directory.GetDirectories(rootDir))
    {
        if (ShouldSkipDirectory(dir))
        {
            continue;
        }

        // Skip if directory contains a .csproj file
        if (Directory.GetFiles(dir, "*.csproj").Length > 0)
        {
            continue;
        }

        foreach (var file in Directory.GetFiles(dir, "*.cs"))
        {
            var fullPath = Path.GetFullPath(file);
            if (!IsIgnoredByGit(fullPath))
            {
                yield return fullPath;
            }
        }

        foreach (var file in EnumerateCandidateCsFiles(dir))
        {
            yield return file;
        }
    }
}

IEnumerable<string> ReadIncludeDirectives(string csFilePath)
{
    var includePaths = new List<string>();

    try
    {
        foreach (var line in File.ReadLines(csFilePath))
        {
            var trimmed = line.Trim();
            const string includeDirective = "#:include";

            if (!trimmed.StartsWith(includeDirective, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var includePath = trimmed[includeDirective.Length..].Trim();
            if (includePath.Length == 0)
            {
                continue;
            }

            includePaths.Add(TrimIncludePath(includePath));
        }
    }
    catch (IOException)
    {
        // If we can't read the file, treat it as having no include directives.
    }
    catch (UnauthorizedAccessException)
    {
        // If we can't read the file, treat it as having no include directives.
    }

    return includePaths;
}

string TrimIncludePath(string includePath)
{
    var commentIndex = includePath.IndexOf("//", StringComparison.Ordinal);
    if (commentIndex >= 0)
    {
        includePath = includePath[..commentIndex].TrimEnd();
    }

    return includePath.Trim().Trim('"', '\'');
}

IEnumerable<string> ResolveIncludePath(string includingFilePath, string includePath)
{
    var includingDirectory = Path.GetDirectoryName(includingFilePath) ?? Environment.CurrentDirectory;
    var fullIncludePath = Path.GetFullPath(Path.Combine(includingDirectory, includePath));

    if (HasWildcard(includePath))
    {
        var searchDirectory = Path.GetDirectoryName(fullIncludePath) ?? includingDirectory;
        var searchPattern = Path.GetFileName(fullIncludePath);

        if (!Directory.Exists(searchDirectory))
        {
            yield break;
        }

        foreach (var match in Directory.GetFiles(searchDirectory, searchPattern))
        {
            yield return Path.GetFullPath(match);
        }
    }
    else if (File.Exists(fullIncludePath))
    {
        yield return fullIncludePath;
    }
}

bool HasWildcard(string path) => path.Contains('*') || path.Contains('?');

List<string> FindExecutableCsFiles(string rootDir, HashSet<string> includedFiles)
{
    var files = new List<string>();
    
    foreach (var dir in Directory.GetDirectories(rootDir))
    {
        if (ShouldSkipDirectory(dir))
        {
            continue;
        }

        // Skip if directory contains a .csproj file
        if (Directory.GetFiles(dir, "*.csproj").Length > 0)
        {
            continue;
        }
        
        // Add all .cs files in this directory (that shouldn't be skipped)
        foreach (var file in Directory.GetFiles(dir, "*.cs"))
        {
            var fullPath = Path.GetFullPath(file);
            if (!IsIgnoredByGit(fullPath) && !includedFiles.Contains(fullPath) && !ShouldSkipFile(fullPath))
            {
                files.Add(fullPath);
            }
        }
        
        // Recursively search subdirectories
        files.AddRange(FindExecutableCsFiles(dir, includedFiles));
    }
    
    return files;
}

bool ShouldSkipDirectory(string directoryPath)
{
    var directoryName = Path.GetFileName(directoryPath);

    return directoryName.StartsWith('.') || IsIgnoredByGit(directoryPath);
}

bool IsIgnoredByGit(string path)
{
    var fullPath = Path.GetFullPath(path);
    if (ignoredPathCache.TryGetValue(fullPath, out var isIgnored))
    {
        return isIgnored;
    }

    var relativePath = Path.GetRelativePath(scriptDir, fullPath);
    if (relativePath.StartsWith(".."))
    {
        ignoredPathCache[fullPath] = false;
        return false;
    }

    var startInfo = new ProcessStartInfo
    {
        FileName = "git",
        WorkingDirectory = scriptDir,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true
    };

    startInfo.ArgumentList.Add("check-ignore");
    startInfo.ArgumentList.Add("-q");
    startInfo.ArgumentList.Add("--");
    startInfo.ArgumentList.Add(relativePath);

    try
    {
        using var process = Process.Start(startInfo);
        if (process is null)
        {
            ignoredPathCache[fullPath] = false;
            return false;
        }

        if (!process.WaitForExit(TimeSpan.FromSeconds(2)))
        {
            process.Kill();
            ignoredPathCache[fullPath] = false;
            return false;
        }

        isIgnored = process.ExitCode == 0;
    }
    catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
    {
        isIgnored = false;
    }

    ignoredPathCache[fullPath] = isIgnored;
    return isIgnored;
}

async Task<List<VerificationResult>> VerifyFilesSequential(List<string> files, int timeoutSeconds)
{
    var results = new List<VerificationResult>();
    
    await AnsiConsole.Progress()
        .Columns(
            new TaskDescriptionColumn(),
            new ProgressBarColumn(),
            new SpinnerColumn())
        .StartAsync(async ctx =>
        {
            var task = ctx.AddTask($"[green]Verifying files (0/{files.Count})[/]", maxValue: files.Count);
            
            foreach (var file in files)
            {
                var result = await VerifyFile(file, timeoutSeconds);
                results.Add(result);
                task.Increment(1);
                task.Description = $"[green]Verifying files ({results.Count}/{files.Count})[/]";
            }
        });
    
    return results;
}

async Task<List<VerificationResult>> VerifyFilesParallel(List<string> files, int timeoutSeconds)
{
    var results = new List<VerificationResult>();
    var lockObj = new Lock();
    
    await AnsiConsole.Progress()
        .Columns(
            new TaskDescriptionColumn(),
            new ProgressBarColumn(),
            new SpinnerColumn())
        .StartAsync(async ctx =>
        {
            var progressTask = ctx.AddTask($"[green]Verifying files (0/{files.Count})[/]", maxValue: files.Count);
            
            var tasks = files.Select(async file =>
            {
                var result = await VerifyFile(file, timeoutSeconds);
                lock (lockObj)
                {
                    results.Add(result);
                    progressTask.Increment(1);
                    progressTask.Description = $"[green]Verifying files ({results.Count}/{files.Count})[/]";
                }
            });
            
            await Task.WhenAll(tasks);
        });
    
    return results;
}

async Task<VerificationResult> VerifyFile(string filePath, int timeoutSeconds)
{
    var result = new VerificationResult
    {
        FilePath = filePath,
        FileName = Path.GetFileName(filePath)
    };
    
    var startInfo = new ProcessStartInfo
    {
        FileName = "dotnet",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        RedirectStandardInput = true,
        UseShellExecute = false,
        CreateNoWindow = true,
        WorkingDirectory = Path.GetDirectoryName(filePath) ?? Environment.CurrentDirectory
    };
    
    // Add arguments using ArgumentList to avoid escaping issues
    startInfo.ArgumentList.Add(filePath);
    
    // Check for .run.json with verify launch profile
    var hasVerifyProfile = HasVerifyLaunchProfile(filePath);
    if (hasVerifyProfile)
    {
        startInfo.ArgumentList.Add("--launch-profile");
        startInfo.ArgumentList.Add("verify");
        result.UsedVerifyProfile = true;
    }

    startInfo.Environment["VERIFY_MODE"] = "1";
    
    if (OperatingSystem.IsWindows())
    {
        startInfo.CreateNewProcessGroup = true;
    }
    
    var process = new Process { StartInfo = startInfo };
    var output = new List<string>();
    var error = new List<string>();
    var shutdownMessageDetected = false;
    var shutdownTcs = new TaskCompletionSource<bool>();
    
    process.OutputDataReceived += (s, e) =>
    {
        if (e.Data != null)
        {
            lock (output)
            {
                output.Add(e.Data);
                if (e.Data.Contains("Press Ctrl+C to shut down.") && !shutdownMessageDetected)
                {
                    shutdownMessageDetected = true;
                    shutdownTcs.TrySetResult(true);
                }
            }
        }
    };
    
    process.ErrorDataReceived += (s, e) =>
    {
        if (e.Data != null)
        {
            lock (error)
            {
                error.Add(e.Data);
            }
        }
    };
    
    var stopwatch = Stopwatch.StartNew();
    
    try
    {
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        
        // Wait for either: process exit, shutdown message detected, or timeout
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);
        var timeoutTask = Task.Delay(timeout);
        var processTask = Task.Run(process.WaitForExit);

        var completedTask = await Task.WhenAny(processTask, shutdownTcs.Task, timeoutTask);
        
        if (completedTask == shutdownTcs.Task)
        {
            // Shutdown message detected - send shutdown signal
            try
            {
                process.Stop();
                
                if (!process.HasExited)
                {
                    process.Kill();
                }
                
                stopwatch.Stop();
                result.Success = true;
                result.ExitCode = 0;
                result.Duration = stopwatch.Elapsed;
                
                result.FullOutput = string.Join("\n", output);
                result.FullError = string.Join("\n", error);
                result.HasStderr = error.Count > 0;
                result.Message = result.HasStderr ? "App started & stopped successfully" : "App started & stopped successfully";
            }
            catch
            {
                process.Kill();
                stopwatch.Stop();
                result.Success = false;
                result.Duration = stopwatch.Elapsed;
                result.Message = "Failed to gracefully stop app";
                
                result.FullOutput = string.Join("\n", output);
                result.FullError = string.Join("\n", error);
            }
        }
        else if (completedTask == timeoutTask)
        {
            // Process timed out without shutdown message
            var allOutput = string.Join("\n", output);
            var allError = string.Join("\n", error);
            
            result.FullOutput = allOutput;
            result.FullError = allError;
            
            process.Kill();
            stopwatch.Stop();
            result.Success = false;
            result.Duration = stopwatch.Elapsed;
            result.Message = $"Timeout ({timeout.TotalSeconds}s)";
        }
        else
        {
            // Process completed normally
            stopwatch.Stop();
            result.ExitCode = process.ExitCode;
            result.Duration = stopwatch.Elapsed;
            result.Success = process.ExitCode == 0;
            
            result.FullOutput = string.Join("\n", output);
            result.FullError = string.Join("\n", error);
            result.HasStderr = error.Count > 0;
            
            if (result.Success)
            {
                result.Message = "Completed successfully";
            }
            else if (error.Count > 0)
            {
                result.Message = string.Join("; ", error.Take(2));
            }
            else
            {
                result.Message = $"Exit code: {process.ExitCode}";
            }
        }
    }
    catch (Exception ex)
    {
        stopwatch.Stop();
        result.Success = false;
        result.Duration = stopwatch.Elapsed;
        result.Message = $"Error: {ex.Message}";
    }
    finally
    {
        if (!process.HasExited)
        {
            try { process.Kill(); } catch { }
        }
        process.Dispose();
    }
    
    return result;
}

void DisplayResults(List<VerificationResult> results)
{
    var table = new Table();
    table.Border(TableBorder.Rounded);
    table.AddColumn("[bold]File (repo-relative)[/]");
    table.AddColumn("[bold]Status[/]");
    table.AddColumn("[bold]Duration[/]");
    table.AddColumn("[bold]Message[/]");
    
    foreach (var result in results.OrderBy(r => GetRepoRelativeDisplayPath(r.FilePath)))
    {
        var statusText = result.Success 
            ? (result.HasStderr ? "[green]✓ Pass (stderr)[/]" : "[green]✓ Pass[/]") 
            : $"[red]✗ Fail ({result.ExitCode})[/]";
        
        var durationText = $"{result.Duration.TotalSeconds:F2}s";
        
        var fileDisplay = GetRepoRelativeDisplayPath(result.FilePath).EscapeMarkup();
        var fileNameDisplay = result.UsedVerifyProfile
            ? $"{fileDisplay} [dim](verify profile)[/]"
            : fileDisplay;
        
        // For failed apps, show full error output; for successful apps, show brief message
        string messageText;
        if (!result.Success)
        {
            // Combine error and output for failed cases
            var fullMessage = !string.IsNullOrEmpty(result.FullError) 
                ? result.FullError 
                : result.FullOutput;
            
            messageText = !string.IsNullOrEmpty(fullMessage) 
                ? fullMessage 
                : result.Message;
        }
        else
        {
            messageText = result.Message.Length > 60 
                ? result.Message.Substring(0, 57) + "..." 
                : result.Message;
        }
        
        table.AddRow(
            fileNameDisplay,
            statusText,
            durationText,
            messageText.EscapeMarkup()
        );
    }
    
    AnsiConsole.Write(table);
    AnsiConsole.WriteLine();
    
    // Summary
    var totalCount = results.Count;
    var passCount = results.Count(r => r.Success);
    var failCount = totalCount - passCount;
    
    var summaryTable = new Table();
    summaryTable.Border(TableBorder.None);
    summaryTable.HideHeaders();
    summaryTable.AddColumn("");
    summaryTable.AddColumn("");
    
    summaryTable.AddRow("[bold]Total:[/]", totalCount.ToString());
    summaryTable.AddRow("[green]Passed:[/]", passCount.ToString());
    if (failCount > 0)
    {
        summaryTable.AddRow("[red]Failed:[/]", failCount.ToString());
    }
    
    AnsiConsole.Write(summaryTable);
    
    if (failCount == 0)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green bold]All tests passed! ✓[/]");
    }
}

string GetRepoRelativeDisplayPath(string filePath)
{
    var relativePath = Path.GetRelativePath(scriptDir, filePath);
    return relativePath.StartsWith("..", StringComparison.Ordinal)
        ? filePath
        : relativePath;
}

class VerificationResult
{
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public bool Success { get; set; }
    public int ExitCode { get; set; }
    public TimeSpan Duration { get; set; }
    public string Message { get; set; } = "";
    public string FullOutput { get; set; } = "";
    public string FullError { get; set; } = "";
    public bool HasStderr { get; set; }
    public bool UsedVerifyProfile { get; set; }
}

internal static partial class ProcessExtensions
{
    // Code in this class adapted from https://github.com/devlooped/dotnet-stop
    // See THIRDPARTYNOTICES for license information.
    extension(Process process)
    {
        public int Stop(TimeSpan? timeout = null, bool quiet = true)
        {
            timeout ??= TimeSpan.FromSeconds(1);
            if (OperatingSystem.IsWindows())
            {
                return process.StopWindowsProcess(timeout.Value, quiet);
            }
            else
            {
                return process.StopUnixProcess(timeout.Value, quiet);
            }
        }

        int StopUnixProcess(TimeSpan timeout, bool quiet)
        {
            if (!quiet)
            {
                AnsiConsole.MarkupLine($"[yellow]Shutting down {process.ProcessName}:{process.Id}...[/]");
            }

            var killProcess = new ProcessStartInfo("kill")
            {
                UseShellExecute = true
            };
            killProcess.ArgumentList.Add("-s");
            killProcess.ArgumentList.Add("SIGINT");
            killProcess.ArgumentList.Add(process.Id.ToString());
            Process.Start(killProcess)?.WaitForExit();

            if (timeout != TimeSpan.Zero)
            {
                if (process.WaitForExit(timeout))
                {
                    return 0;
                }

                if (!quiet)
                {
                    AnsiConsole.MarkupLine($"[red]Timed out waiting for process {process.ProcessName}:{process.Id} to exit[/]");
                }

                return -1;
            }
            else
            {
                process.WaitForExit();
                return 0;
            }
        }

        int StopWindowsProcess(TimeSpan timeout, bool quiet)
        {
            if (!quiet)
            {
                AnsiConsole.MarkupLine($"[yellow]Shutting down {process.ProcessName}:{process.Id}...[/]");
            }

            // Send Ctrl+Break to the process group
            GenerateConsoleCtrlEvent(1, (uint)process.Id);

            if (timeout != TimeSpan.Zero)
            {
                if (process.WaitForExit(timeout))
                {
                    return 0;
                }

                if (!quiet)
                {
                    AnsiConsole.MarkupLine($"[red]Timed out waiting for process {process.ProcessName}:{process.Id} to exit[/]");
                }

                return -1;
            }
            else
            {
                process.WaitForExit();
                return 0;
            }
        }
    }

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);
}

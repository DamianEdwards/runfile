#!/bin/usr/env dotnet
#:package Spectre.Console@0.57.2

using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Spectre.Console;

// Parse command-line arguments
var useParallel = false;
var verbose = false;
var noCache = false;
var selfTest = false;
string? selectedFile = null;
var timeoutSeconds = 10; // Default timeout
for (int i = 0; i < args.Length; i++)
{
    switch (args[i].ToLowerInvariant())
    {
        case "--self-test":
            selfTest = true;
            break;
        case "--parallel":
            useParallel = true;
            break;
        case "--verbose":
            verbose = true;
            break;
        case "--no-cache":
            noCache = true;
            break;
        case "--file":
            if (++i >= args.Length || string.IsNullOrWhiteSpace(args[i]) || args[i].StartsWith("--"))
            {
                AnsiConsole.MarkupLine("[red]Error: --file requires a file path.[/]");
                return 1;
            }
            selectedFile = args[i];
            break;
        case "--timeout":
            if (++i >= args.Length || !int.TryParse(args[i], out timeoutSeconds) || timeoutSeconds <= 0)
            {
                AnsiConsole.MarkupLine("[red]Error: --timeout requires a positive number of seconds.[/]");
                return 1;
            }
            break;
        case "--help":
        case "-h":
            AnsiConsole.WriteLine("Usage: dotnet verify.cs -- [--file <path>] [--parallel] [--timeout <seconds>] [--verbose] [--no-cache] [--self-test]");
            AnsiConsole.WriteLine("--file paths are relative to the current working directory.");
            AnsiConsole.WriteLine("--self-test runs dependency grouping regression tests instead of verifying apps.");
            return 0;
        default:
            AnsiConsole.MarkupLine($"[red]Error: Unknown argument '{args[i].EscapeMarkup()}'. Use --help for usage.[/]");
            return 1;
    }
}

if (selfTest)
{
    RunGroupingTests();
    return 0;
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

if (selectedFile is not null)
{
    string fullPath;
    try
    {
        fullPath = Path.GetFullPath(selectedFile);
    }
    catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
    {
        AnsiConsole.MarkupLine($"[red]Error: Invalid file path: {ex.Message.EscapeMarkup()}[/]");
        return 1;
    }

    var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    csFiles = csFiles.Where(file => comparer.Equals(file, fullPath)).ToList();
    if (csFiles.Count == 0)
    {
        AnsiConsole.MarkupLine($"[red]Error: '{selectedFile.EscapeMarkup()}' is not an executable verification candidate on this platform.[/]");
        return 1;
    }
}

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
if (verbose)
{
    AnsiConsole.WriteLine($"Runtime: {RuntimeInformation.FrameworkDescription}; OS: {RuntimeInformation.OSDescription}");
    AnsiConsole.WriteLine($"CI: {Environment.GetEnvironmentVariable("CI") ?? "(unset)"}; GITHUB_ACTIONS: {Environment.GetEnvironmentVariable("GITHUB_ACTIONS") ?? "(unset)"}");
    AnsiConsole.WriteLine($"Redirected streams: stdin={Console.IsInputRedirected}, stdout={Console.IsOutputRedirected}, stderr={Console.IsErrorRedirected}");
}
AnsiConsole.WriteLine();

// Run verification
List<VerificationResult> results;
try
{
    results = useParallel
        ? await VerifyFilesParallel(csFiles, timeoutSeconds)
        : await VerifyFilesSequential(csFiles, timeoutSeconds);
}
catch (InvalidOperationException ex)
{
    AnsiConsole.MarkupLine($"[red]Verification failed: {ex.Message.EscapeMarkup()}[/]");
    return 1;
}

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

IEnumerable<string> ReadIncludeDirectives(string csFilePath) => ReadPathDirectives(csFilePath, "#:include");

IEnumerable<string> ReadPathDirectives(string csFilePath, string directive)
{
    var includePaths = new List<string>();

    try
    {
        foreach (var line in File.ReadLines(csFilePath))
        {
            var trimmed = line.Trim();

            if (!trimmed.StartsWith(directive, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var includePath = trimmed[directive.Length..].Trim();
            if (includePath.Length == 0)
            {
                continue;
            }

            includePaths.Add(TrimIncludePath(includePath));
        }
    }
    catch (IOException ex)
    {
        if (directive != "#:include")
        {
            throw new InvalidOperationException($"Cannot read build dependencies from '{csFilePath}'.", ex);
        }
        // If we can't read the file, treat it as having no include directives.
    }
    catch (UnauthorizedAccessException ex)
    {
        if (directive != "#:include")
        {
            throw new InvalidOperationException($"Cannot read build dependencies from '{csFilePath}'.", ex);
        }
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

    // Files linked via #:ref/#:project share build outputs (building a referencing app also builds
    // its references), so each linked group runs sequentially while groups run in parallel.
    var groups = GroupFilesByReferences(files);
    if (verbose)
    {
        foreach (var group in groups.Where(g => g.Count > 1))
        {
            AnsiConsole.WriteLine($"Sequential group: {string.Join(" -> ", group.Select(f => Path.GetRelativePath(scriptDir, f)))}");
        }
    }

    await AnsiConsole.Progress()
        .Columns(
            new TaskDescriptionColumn(),
            new ProgressBarColumn(),
            new SpinnerColumn())
        .StartAsync(async ctx =>
        {
            var progressTask = ctx.AddTask($"[green]Verifying files (0/{files.Count})[/]", maxValue: files.Count);

            var tasks = groups.Select(async group =>
            {
                foreach (var file in group)
                {
                    var result = await VerifyFile(file, timeoutSeconds);
                    lock (lockObj)
                    {
                        results.Add(result);
                        progressTask.Increment(1);
                        progressTask.Description = $"[green]Verifying files ({results.Count}/{files.Count})[/]";
                    }
                }
            });

            await Task.WhenAll(tasks);
        });

    return results;
}

// Groups files that share build outputs: any two files whose transitive #:ref/#:project
// dependency graphs touch the same node (whether or not that node is itself a selected file).
List<List<string>> GroupFilesByReferences(List<string> files)
{
    var comparer = StringComparer.OrdinalIgnoreCase;
    var fileSet = new HashSet<string>(files, comparer);
    var dependencies = new Dictionary<string, List<string>>(comparer);
    var parent = new Dictionary<string, string>(comparer);

    string Find(string node)
    {
        if (!parent.TryGetValue(node, out var p))
        {
            parent[node] = node;
            return node;
        }

        return comparer.Equals(p, node) ? node : parent[node] = Find(p);
    }

    var pending = new Queue<string>(files);
    while (pending.TryDequeue(out var node))
    {
        if (dependencies.ContainsKey(node))
        {
            continue;
        }

        var deps = GetBuildDependencies(node);
        dependencies[node] = deps;

        foreach (var dep in deps)
        {
            parent[Find(node)] = Find(dep);
            pending.Enqueue(dep);
        }
    }

    var groups = new Dictionary<string, List<string>>(comparer);
    var visited = new HashSet<string>(comparer);

    // Referenced files are verified before the files that (transitively) reference them.
    void Visit(string node, List<string> ordered)
    {
        if (!visited.Add(node))
        {
            return;
        }

        foreach (var dep in dependencies[node])
        {
            Visit(dep, ordered);
        }

        if (fileSet.Contains(node))
        {
            ordered.Add(node);
        }
    }

    foreach (var file in files)
    {
        var root = Find(file);
        if (!groups.TryGetValue(root, out var ordered))
        {
            groups[root] = ordered = [];
        }

        Visit(file, ordered);
    }

    return groups.Values.ToList();
}

List<string> GetBuildDependencies(string path)
{
    var deps = new List<string>();

    if (path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
    {
        foreach (var refPath in ReadPathDirectives(path, "#:ref"))
        {
            var resolved = ResolveIncludePath(path, NormalizeSeparators(refPath)).ToList();
            if (resolved.Count == 0)
            {
                throw new InvalidOperationException($"Cannot resolve build dependency '{refPath}' from '{path}'.");
            }
            deps.AddRange(resolved);
        }

        foreach (var projectPath in ReadPathDirectives(path, "#:project"))
        {
            deps.Add(ResolveProjectPath(path, projectPath));
        }
    }
    else if (path.EndsWith("proj", StringComparison.OrdinalIgnoreCase))
    {
        try
        {
            var projectRefs = System.Xml.Linq.XDocument.Load(path).Descendants()
                .Where(e => e.Name.LocalName == "ProjectReference")
                .Select(e => e.Attribute("Include")?.Value)
                .OfType<string>();

            foreach (var projectRef in projectRefs)
            {
                deps.Add(ResolveProjectPath(path, projectRef));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
        {
            throw new InvalidOperationException($"Cannot read build dependencies from project '{path}'.", ex);
        }
    }

    return deps;
}

string ResolveProjectPath(string referencingPath, string projectPath)
{
    var baseDirectory = Path.GetDirectoryName(referencingPath) ?? Environment.CurrentDirectory;
    var fullPath = Path.GetFullPath(Path.Combine(baseDirectory, NormalizeSeparators(projectPath)));

    if (Directory.Exists(fullPath))
    {
        var projectFiles = Directory.GetFiles(fullPath, "*.*proj");
        if (projectFiles.Length != 1)
        {
            throw new InvalidOperationException($"Cannot identify a single project in build dependency '{fullPath}'.");
        }
        return Path.GetFullPath(projectFiles[0]);
    }

    return fullPath;
}

string NormalizeSeparators(string path) => path.Replace('\\', '/');

void RunGroupingTests()
{
    var root = Path.Combine(Path.GetTempPath(), $"runfile-grouping-tests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);
    try
    {
        string Write(string name, string content = "")
        {
            var path = Path.Combine(root, name);
            File.WriteAllText(path, content);
            return path;
        }

        void Check(string name, List<string> files, params string[][] expected)
        {
            var actual = GroupFilesByReferences(files)
                .Select(group => string.Join(",", group.Select(Path.GetFileName)))
                .Order(StringComparer.Ordinal).ToArray();
            var wanted = expected.Select(group => string.Join(",", group))
                .Order(StringComparer.Ordinal).ToArray();
            if (!actual.SequenceEqual(wanted))
            {
                throw new InvalidOperationException($"{name}: expected [{string.Join("; ", wanted)}], got [{string.Join("; ", actual)}].");
            }
            AnsiConsole.WriteLine($"PASS: {name}");
        }

        void MustFail(string name, Action action)
        {
            try
            {
                action();
            }
            catch (InvalidOperationException)
            {
                AnsiConsole.WriteLine($"PASS: {name}");
                return;
            }
            throw new InvalidOperationException($"{name}: dependency inspection unexpectedly succeeded.");
        }

        var dependency = Write("dependency.cs");
        var direct = Write("direct.cs", "#:ref ./dependency.cs");
        var independent = Write("independent.cs");
        Check("direct reference, dependency first, independent file",
            [direct, independent, dependency], ["dependency.cs", "direct.cs"], ["independent.cs"]);

        var sibling = Write("sibling.cs", "#:ref ./dependency.cs");
        Check("shared noncandidate file",
            [direct, sibling, independent], ["direct.cs", "sibling.cs"], ["independent.cs"]);

        var middle = Write("middle.cs", "#:ref ./dependency.cs");
        var transitive = Write("transitive.cs", "#:ref ./middle.cs");
        Check("transitive noncandidate file",
            [transitive, sibling, independent], ["transitive.cs", "sibling.cs"], ["independent.cs"]);
        Check("transitive selected files, dependency first",
            [transitive, dependency, middle], ["dependency.cs", "middle.cs", "transitive.cs"]);

        Write("base.csproj", "<Project />");
        Write("library.csproj", "<Project><ItemGroup><ProjectReference Include=\"base.csproj\" /></ItemGroup></Project>");
        var projectApp = Write("project.cs", "#:project ./library.csproj");
        var sharedApp = Write("shared.cs", "#:project ./library.csproj");
        var baseApp = Write("base.cs", "#:project ./base.csproj");
        Check("shared noncandidate project",
            [projectApp, sharedApp, independent], ["project.cs", "shared.cs"], ["independent.cs"]);
        Check("transitive project dependency",
            [projectApp, baseApp, independent], ["project.cs", "base.cs"], ["independent.cs"]);

        var includes = Write("includes.cs", "#:include \"dependency.cs\" // comment\n#:include\tmiddle.cs");
        if (!ReadIncludeDirectives(includes).SequenceEqual(["dependency.cs", "middle.cs"]))
        {
            throw new InvalidOperationException("Include directive parsing changed.");
        }
        AnsiConsole.WriteLine("PASS: include directive compatibility");

        Write("broken.csproj", "<Project>");
        var broken = Write("broken.cs", "#:project ./broken.csproj");
        MustFail("malformed project XML fails scheduling", () => GroupFilesByReferences([broken, independent]));
        MustFail("unreadable source fails scheduling",
            () => GroupFilesByReferences([Path.Combine(root, "missing.cs"), independent]));
        MustFail("unreadable project fails inspection",
            () => GetBuildDependencies(Path.Combine(root, "missing.csproj")));
        var missingRef = Write("missing-ref.cs", "#:ref ./missing.cs");
        MustFail("missing referenced source fails scheduling",
            () => GroupFilesByReferences([missingRef, independent]));
        var missingProject = Write("missing-project.cs", "#:project ./missing.csproj");
        MustFail("missing referenced project fails scheduling",
            () => GroupFilesByReferences([missingProject, independent]));
        using (var locked = new FileStream(dependency, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            MustFail("locked source fails scheduling", () => GroupFilesByReferences([dependency, independent]));
        }
        using (var locked = new FileStream(Path.Combine(root, "library.csproj"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            MustFail("locked project fails scheduling", () => GroupFilesByReferences([projectApp, independent]));
        }
        AnsiConsole.WriteLine("All dependency grouping regression tests passed.");
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
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
    if (noCache)
    {
        startInfo.ArgumentList.Add("--no-cache");
    }

    // Check for .run.json with verify launch profile
    var hasVerifyProfile = HasVerifyLaunchProfile(filePath);
    if (hasVerifyProfile)
    {
        startInfo.ArgumentList.Add("--launch-profile");
        startInfo.ArgumentList.Add("verify");
        result.UsedVerifyProfile = true;
    }
    else if (IsWebFileApp(filePath))
    {
        startInfo.ArgumentList.Add("--no-launch-profile");
        startInfo.ArgumentList.Add("--");
        startInfo.ArgumentList.Add("--urls");
        startInfo.ArgumentList.Add("http://127.0.0.1:0");
    }

    startInfo.Environment["VERIFY_MODE"] = "1";
    startInfo.Environment["NO_COLOR"] = "1";
    result.Command = $"dotnet {string.Join(" ", startInfo.ArgumentList.Select(argument => $"\"{argument}\""))}";

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
                // Some CLI output retains ANSI styling even with NO_COLOR set.
                var plainOutput = Regex.Replace(e.Data, @"\x1b\[[0-?]*[ -/]*[@-~]", "");
                // Aspire CLI-launched AppHosts print "Press CTRL+C to stop the AppHost and exit."
                if ((plainOutput.Contains("Press Ctrl+C to shut down.") || plainOutput.Contains("Press CTRL+C to stop the AppHost"))
                    && !shutdownMessageDetected)
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

bool IsWebFileApp(string csFilePath)
{
    try
    {
        foreach (var line in File.ReadLines(csFilePath))
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("#:sdk", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed.Contains("Microsoft.NET.Sdk.Web", StringComparison.OrdinalIgnoreCase);
            }

            if (!string.IsNullOrWhiteSpace(trimmed) &&
                !trimmed.StartsWith("#") &&
                !trimmed.StartsWith("//") &&
                !trimmed.StartsWith("/*"))
            {
                return false;
            }
        }
    }
    catch
    {
        // If we can't read the file, verify it without web-specific arguments.
    }

    return false;
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
            : result.ExitCode is int exitCode
                ? $"[red]✗ Fail ({exitCode})[/]"
                : "[red]✗ Fail[/]";

        var durationText = $"{result.Duration.TotalSeconds:F2}s";

        var fileDisplay = GetRepoRelativeDisplayPath(result.FilePath).EscapeMarkup();
        var fileNameDisplay = result.UsedVerifyProfile
            ? $"{fileDisplay} [dim](verify profile)[/]"
            : fileDisplay;

        var messageText = result.Message.Length > 60
            ? result.Message.Substring(0, 57) + "..."
            : result.Message;

        table.AddRow(
            fileNameDisplay,
            statusText,
            durationText,
            messageText.EscapeMarkup()
        );
    }

    AnsiConsole.Write(table);
    AnsiConsole.WriteLine();

    foreach (var result in results.Where(result => !result.Success || verbose)
        .OrderBy(result => GetRepoRelativeDisplayPath(result.FilePath)))
    {
        AnsiConsole.WriteLine($"--- {GetRepoRelativeDisplayPath(result.FilePath)} ---");
        AnsiConsole.WriteLine($"Result: {result.Message} ({result.Duration.TotalSeconds:F2}s); exit code: {result.ExitCode?.ToString() ?? "(not exited)"}");
        AnsiConsole.WriteLine($"Command: {result.Command}");
        AnsiConsole.WriteLine($"Working directory: {Path.GetDirectoryName(result.FilePath)}");
        AnsiConsole.WriteLine("stdout:");
        AnsiConsole.WriteLine(string.IsNullOrEmpty(result.FullOutput) ? "(empty)" : result.FullOutput);
        AnsiConsole.WriteLine("stderr:");
        AnsiConsole.WriteLine(string.IsNullOrEmpty(result.FullError) ? "(empty)" : result.FullError);
        AnsiConsole.WriteLine();
    }

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
    public int? ExitCode { get; set; }
    public string Command { get; set; } = "";
    public TimeSpan Duration { get; set; }
    public string Message { get; set; } = "";
    public string FullOutput { get; set; } = "";
    public string FullError { get; set; } = "";
    public bool HasStderr { get; set; }
    public bool UsedVerifyProfile { get; set; }
}

internal static class ProcessExtensions
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

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);
}

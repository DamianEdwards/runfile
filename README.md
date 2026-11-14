# runfile Playground

Playground of file-based C# apps for `dotnet run file.cs` feature added in .NET 10.

Requires [.NET SDK version 10.0.100](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) or later.

## Verification

Run all executable samples with `dotnet verify.cs --parallel --timeout 400`.

To diagnose one sample, use `dotnet verify.cs -- --file flat/apphost.cs --timeout 120 --verbose`.
The `--` separates verifier arguments from .NET CLI options such as `--file`.
The file path is relative to the current working directory and must be an executable
sample discovered by the verifier (not an included helper or a project source file).
Samples still use their `verify` launch profile when available.
The verifier sets `NO_COLOR=1` for child processes and strips any remaining ANSI
styling when matching readiness messages; its own report can still use color.
Add `--no-cache` to rebuild the selected samples instead of using the .NET file-based
app cache, for example when comparing a local run with a fresh CI runner.

Failures always print the reason, command, working directory, and both stdout and stderr
outside the summary table. Add `--verbose` to also print output from successful samples
and basic runtime/CI diagnostics. Use `dotnet verify.cs -- --help` to see the available options.

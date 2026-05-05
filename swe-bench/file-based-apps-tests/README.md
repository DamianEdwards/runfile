# File-based app scenario test source

This folder contains normal C# test source for the scenarios in `../file-based-apps-scenarios.md`.

These files are not intended to be a single test suite that passes against the current base commit. Each scenario should use the relevant test file(s), plus `TestInfrastructure.cs` and `Runfile.SweBench.Tests.csproj`, to verify behavior after an agent applies the scenario prompt.

Tests run against the directory specified by the `RUNFILE_SCENARIO_ROOT` environment variable. If that variable is not set, tests default to `../file-based-apps-starters/base/` for local convenience.

Recommended workflow:

```powershell
Copy-Item -Recurse swe-bench\file-based-apps-starters\base .work\scenario
$env:RUNFILE_SCENARIO_ROOT = (Resolve-Path .work\scenario)
dotnet test swe-bench\file-based-apps-tests\Runfile.SweBench.Tests.csproj --filter "FullyQualifiedName~BasicGreeterTests"
```

For example, `BasicGreeterTests.cs` is expected to fail against the unmodified `base` starter because `flat/greet.cs` does not exist yet. It should pass after the create-greeter scenario is implemented in the copied workspace.

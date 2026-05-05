# File-based app starter workspaces

These folders contain self-contained starter content for the scenarios in `../file-based-apps-scenarios.md`.

| Starter | Use for |
| --- | --- |
| `base/` | Most scenarios, including creating `flat/greet.cs`, extending `cat`, updating `webapi`, user secrets, launch profiles, project references, and Razor diagnostics. |
| `greeter-humanizer/` | The follow-up scenario that starts from an existing basic `flat/greet.cs` app and asks the agent to add Humanizer support. |

Copy the appropriate starter folder to a working directory before giving the scenario prompt to an agent. Run tests by setting `RUNFILE_SCENARIO_ROOT` to that working directory.

```powershell
Copy-Item -Recurse swe-bench\file-based-apps-starters\base .work\scenario
$env:RUNFILE_SCENARIO_ROOT = (Resolve-Path .work\scenario)
dotnet test swe-bench\file-based-apps-tests\Runfile.SweBench.Tests.csproj --filter "FullyQualifiedName~BasicGreeterTests"
```

The tests default to `base/` only as a convenience for local compile/discovery checks. Scenario validation should set `RUNFILE_SCENARIO_ROOT` explicitly.

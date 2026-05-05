# SWE-Bench scenario pack: .NET file-based apps

This pack proposes file-based app scenarios for `SWE-Bench-C#-General`. The scenarios are self-contained under `swe-bench/` and include local starter workspaces copied from `DamianEdwards/runfile`, a playground repo for the .NET 10 `dotnet run file.cs` feature.

- **Reference repo:** `DamianEdwards/runfile`
- **Starter source commit:** `ebfa157` (`Add note about using Directory.Build.props for experimental properties`)
- **Starter roots:** `swe-bench/file-based-apps-starters/base/` and `swe-bench/file-based-apps-starters/greeter-humanizer/`
- **Minimum SDK:** .NET SDK `10.0.300-preview` with `rollForward: latestFeature`
- **Reference:** `swe-bench/file-based-apps-ref.md`
- **Primary coverage:** creating file-based apps from scratch, running file apps, `#:package`, `#:project`, `#:property`, `#:sdk`, central package management, build/pack/convert CLI workflows, launch profiles, user secrets, shell execution, single-file Web SDK apps, Razor components, Aspire AppHost SDK usage, and multi-file composition with file app directives.

The prompt text below is intended to be used directly as `problem_statement.txt`. It is intentionally written like a user request, not like a benchmark instruction. For example, the prompts generally do not say "do not add a `.csproj`"; hidden tests should verify whether the agent preserved the file-based app shape when that is the expected solution.

Test source lives in `swe-bench/file-based-apps-tests/`. Each scenario should use the relevant tests from that folder to verify behavior after an agent applies the prompt to a copy of the starting-point code.

## Running scenario tests

Copy the scenario's starter root to a throwaway workspace, run the agent against that copy, then point the tests at the modified copy with `RUNFILE_SCENARIO_ROOT`.

```powershell
Copy-Item -Recurse swe-bench\file-based-apps-starters\base .work\scenario
$env:RUNFILE_SCENARIO_ROOT = (Resolve-Path .work\scenario)
dotnet test swe-bench\file-based-apps-tests\Runfile.SweBench.Tests.csproj --filter "FullyQualifiedName~BasicGreeterTests"
```

If `RUNFILE_SCENARIO_ROOT` is not set, the tests default to `swe-bench/file-based-apps-starters/base/`. That default is useful for local compile/discovery checks, but scenario validation should set the variable explicitly so tests operate over the agent-modified workspace.

## Scenario summary

| Scenario ID | Focus | Starter root | Test filter |
| --- | --- | --- | --- |
| `damianedwards__runfile-fileapps-create-greeter` | Create a basic file-based app from scratch | `file-based-apps-starters/base/` | `FullyQualifiedName~BasicGreeterTests` |
| `damianedwards__runfile-fileapps-greeter-humanizer` | Add a NuGet package directive to a small file app | `file-based-apps-starters/greeter-humanizer/` | `FullyQualifiedName~GreeterHumanizerTests` |
| `damianedwards__runfile-fileapps-central-packages` | Use `Directory.Packages.props` with unversioned `#:package` directives | `file-based-apps-starters/greeter-humanizer/` | `FullyQualifiedName~CentralPackageGreeterTests` |
| `damianedwards__runfile-fileapps-pack-tool` | Prepare and pack a file-based app as a local .NET tool package | `file-based-apps-starters/greeter-humanizer/` | `FullyQualifiedName~ToolPackagingTests` |
| `damianedwards__runfile-fileapps-convert-project` | Convert a file-based app to a project while preserving the original file | `file-based-apps-starters/greeter-humanizer/` | `FullyQualifiedName~ConvertProjectTests` |
| `damianedwards__runfile-fileapps-cat-cli` | Extend a multi-file CLI app with `#:include` and `System.CommandLine` | `file-based-apps-starters/base/` | `FullyQualifiedName~CatCliFeatureTests` |
| `damianedwards__runfile-fileapps-webapi-todos` | Add minimal API CRUD to a single-file Web SDK app | `file-based-apps-starters/base/` | `FullyQualifiedName~WebApiTodoTests` |
| `damianedwards__runfile-fileapps-user-secrets` | Build a configuration diagnostic around file app user secrets | `file-based-apps-starters/base/` | `FullyQualifiedName~UserSecretsConfigTests` |
| `damianedwards__runfile-fileapps-launch-profiles` | Use flat `*.run.json` launch profiles and environment settings | `file-based-apps-starters/base/` | `FullyQualifiedName~LaunchProfileTests` |
| `damianedwards__runfile-fileapps-shell-script` | Add a shebang-ready file app suitable for direct shell execution | `file-based-apps-starters/base/` | `FullyQualifiedName~ShellExecutableTests` |
| `damianedwards__runfile-fileapps-build-output` | Use file app build directives and `dotnet build` output customization | `file-based-apps-starters/base/` | `FullyQualifiedName~BuildOutputTests` |
| `damianedwards__runfile-fileapps-aot-json-publish` | Publish a native AOT-friendly JSON file app using source generation | `file-based-apps-starters/base/` | `FullyQualifiedName~AotJsonPublishTests` |
| `damianedwards__runfile-fileapps-disable-aot-json-publish` | Disable Native AOT for a dynamic JSON utility and publish it | `file-based-apps-starters/base/` | `FullyQualifiedName~DisableAotJsonPublishTests` |
| `damianedwards__runfile-fileapps-aspire-apphost` | Create a file-based Aspire AppHost with a non-default SDK directive | `file-based-apps-starters/base/` | `FullyQualifiedName~AspireAppHostTests` |
| `damianedwards__runfile-fileapps-project-ref` | Enhance a file app that references a class library with `#:project` | `file-based-apps-starters/base/` | `FullyQualifiedName~ProjectRefGreetingTests` |
| `damianedwards__runfile-fileapps-razor-dashboard` | Add routable UI to a file-based Razor/Blazor app | `file-based-apps-starters/base/` | `FullyQualifiedName~RazorDashboardTests` |

## `damianedwards__runfile-fileapps-create-greeter`

- **Expected tech:** .NET 10 file-based app; top-level statements; command-line arguments
- **Starter root:** `swe-bench/file-based-apps-starters/base/`
- **Base files:** none; creates `flat/greet.cs`
- **Suggested tests:** `swe-bench/file-based-apps-tests/BasicGreeterTests.cs`
- **Test filter:** `FullyQualifiedName~BasicGreeterTests`

### Prompt

```text
I want a tiny C# app I can run directly with dotnet for quick greeting demos.

Add it at `flat/greet.cs`. Running it with no arguments should print `Hello, World!`. If I pass a name, it should greet that name instead:

`dotnet run --file flat/greet.cs -- Ada`

should print:

`Hello, Ada!`
```

### Expected outcome

- `flat/greet.cs` exists and can be run with `dotnet run --file flat/greet.cs`.
- No traditional project file is needed for the greeting app.
- The app handles no-argument and single-name invocations.

## `damianedwards__runfile-fileapps-greeter-humanizer`

- **Expected tech:** .NET 10 file-based app; `#:package`; NuGet package usage; command-line arguments
- **Starter root:** `swe-bench/file-based-apps-starters/greeter-humanizer/`
- **Scenario base:** starts from a basic `flat/greet.cs` app
- **Suggested tests:** `swe-bench/file-based-apps-tests/GreeterHumanizerTests.cs`
- **Test filter:** `FullyQualifiedName~GreeterHumanizerTests`

### Prompt

```text
Can you make `flat/greet.cs` a little nicer by using Humanizer?

I'd like names such as `ada lovelace` to be displayed as `Ada Lovelace`. Also add an optional `--count <number>` argument so the app can say which greeting number this is. For example:

`dotnet run --file flat/greet.cs -- --count 3 ada lovelace`

should print:

`Hello, Ada Lovelace!`
`This is your 3rd greeting.`
```

### Expected outcome

- `flat/greet.cs` references Humanizer with a file app package directive.
- The app still works with the original no-argument and single-name usage.
- Names are title-cased.
- `--count` uses ordinal formatting such as `1st`, `2nd`, `3rd`, and `4th`.

## `damianedwards__runfile-fileapps-central-packages`

- **Expected tech:** .NET 10 file-based app; `#:package`; `Directory.Packages.props`; central package management
- **Starter root:** `swe-bench/file-based-apps-starters/greeter-humanizer/`
- **Base files:** `flat/greet.cs`
- **Suggested tests:** `swe-bench/file-based-apps-tests/CentralPackageGreeterTests.cs`
- **Test filter:** `FullyQualifiedName~CentralPackageGreeterTests`

### Prompt

```text
I'm going to add more little C# scripts here, so I'd like package versions managed in one place.

Update `flat/greet.cs` to use Humanizer for title-casing names and ordinalizing the optional `--count` value, but put the Humanizer version in a central package management file instead of hard-coding it in the script. The app should still run with `dotnet run --file flat/greet.cs`.
```

### Expected outcome

- A `Directory.Packages.props` file exists in the scenario root and centrally declares the Humanizer version.
- `flat/greet.cs` uses an unversioned `#:package Humanizer` directive.
- The app still handles the original greeting behavior.
- Names are title-cased and `--count` uses ordinal text.

## `damianedwards__runfile-fileapps-pack-tool`

- **Expected tech:** .NET 10 file-based app; `#:property`; `dotnet pack`; automatic .NET tool packaging
- **Starter root:** `swe-bench/file-based-apps-starters/greeter-humanizer/`
- **Base files:** `flat/greet.cs`
- **Suggested tests:** `swe-bench/file-based-apps-tests/ToolPackagingTests.cs`
- **Test filter:** `FullyQualifiedName~ToolPackagingTests`

### Prompt

```text
I'd like to share `flat/greet.cs` as a local dotnet tool package for demos.

Please add whatever package metadata the file needs so `dotnet pack flat/greet.cs --output <folder>` produces a `.nupkg` with package id `Runfile.Greet`, version `1.0.0`, and tool command name `greet`.
```

### Expected outcome

- `flat/greet.cs` contains package/tool metadata as file app property directives.
- `dotnet pack flat/greet.cs --output <folder>` succeeds.
- The output folder contains a `Runfile.Greet.1.0.0.nupkg` package.

## `damianedwards__runfile-fileapps-convert-project`

- **Expected tech:** .NET 10 file-based app; `dotnet project convert`; preserving original file apps; project output
- **Starter root:** `swe-bench/file-based-apps-starters/greeter-humanizer/`
- **Base files:** `flat/greet.cs`
- **Suggested tests:** `swe-bench/file-based-apps-tests/ConvertProjectTests.cs`
- **Test filter:** `FullyQualifiedName~ConvertProjectTests`

### Prompt

```text
I started with `flat/greet.cs`, but now I want a project version too so I can keep growing it.

Convert the greeting app into a normal project next to the original file app. Keep `flat/greet.cs` in place, and put the converted project under `flat/greet/`. The project version should still support greeting a supplied name.
```

### Expected outcome

- `flat/greet.cs` still exists and still runs as a file-based app.
- A project exists under `flat/greet/`.
- Running the converted project with a name prints the same greeting as the file app.

## `damianedwards__runfile-fileapps-cat-cli`

- **Expected tech:** .NET 10 file-based app; `#:include`; `#:package System.CommandLine`; top-level statements; cancellation; CLI parsing
- **Starter root:** `swe-bench/file-based-apps-starters/base/`
- **Base files:** `flat/cat.cs`, `flat/cat.commands.cs`, `flat/cat.helpers.cs`, `flat/cat.run.json`
- **Suggested tests:** `swe-bench/file-based-apps-tests/CatCliFeatureTests.cs`
- **Test filter:** `FullyQualifiedName~CatCliFeatureTests`

### Prompt

```text
Can you add a few useful options to the `cat` sample in the `flat` folder?

I'd like:

- `--number` / `-n` to prefix each output line with a line number like Unix `cat -n`
- `--squeeze-blank` / `-s` to collapse repeated blank lines
- `--find <text>` to print only matching lines
- `--ignore-case` to make `--find` case-insensitive

If `--find` doesn't match anything, print `No matches found.` to stderr and return exit code 1.
```

### Expected outcome

- The app remains runnable through `flat/cat.cs`.
- Existing split implementation files keep working.
- Existing `--show-line-count` behavior still works and counts output lines.
- Line numbering, blank-line squeezing, and filtering can be combined.

## `damianedwards__runfile-fileapps-webapi-todos`

- **Expected tech:** .NET 10 file-based app; `#:sdk Microsoft.NET.Sdk.Web`; ASP.NET Core minimal APIs; OpenAPI; System.Text.Json source generation
- **Starter root:** `swe-bench/file-based-apps-starters/base/`
- **Base files:** `flat/webapi.cs`, `flat/webapi.run.json`, `flat/webapi.settings.json`
- **Suggested tests:** `swe-bench/file-based-apps-tests/WebApiTodoTests.cs`
- **Test filter:** `FullyQualifiedName~WebApiTodoTests`

### Prompt

```text
Please add a small Todo API to `flat/webapi.cs`.

The todo shape should be:

{ "id": number, "title": string, "isComplete": boolean }

I need endpoints for listing, reading, creating, updating, and deleting todos under `/todos`. Keep the existing `/` hello endpoint working. Titles should be required after trimming, ids should be generated by the server, and the OpenAPI document should show the Todo endpoints when the app is running in Development.
```

### Expected outcome

- `GET /` continues returning `{ "message": "Hello, World!" }`.
- CRUD endpoints work over HTTP with standard status codes.
- Todo storage is in-memory for the process lifetime.
- `/openapi/v1.json` contains `/todos` paths in Development.
- JSON metadata remains compatible with Native AOT.

## `damianedwards__runfile-fileapps-user-secrets`

- **Expected tech:** .NET 10 file-based app; `#:package`; user secrets with `--file`; configuration providers; extension members
- **Starter root:** `swe-bench/file-based-apps-starters/base/`
- **Base files:** `flat/usersecrets.cs`
- **Suggested tests:** `swe-bench/file-based-apps-tests/UserSecretsConfigTests.cs`
- **Test filter:** `FullyQualifiedName~UserSecretsConfigTests`

### Prompt

```text
Can you turn `flat/usersecrets.cs` into a small config lookup tool?

I'd like to pass one or more config keys and have it tell me whether each key was found. It should check command-line overrides like `--set Demo:ApiKey=value`, then environment variables, then user secrets for this file. Don't print secret values unless I pass `--show-values`.

If any requested key is missing, report all keys and exit with code 2. If I don't pass any keys, print short usage text and exit with code 64.
```

### Expected outcome

- User secrets set with `dotnet user-secrets set Some:Key value --file flat/usersecrets.cs` are found.
- Environment variables support `__` nesting.
- `--set` wins over environment variables and user secrets.
- Values are redacted unless `--show-values` is present.
- Missing keys and no-key invocations use the requested exit codes.

## `damianedwards__runfile-fileapps-launch-profiles`

- **Expected tech:** .NET 10 file-based app launch profiles; flat `*.run.json`; ASP.NET Core configuration; minimal APIs
- **Starter root:** `swe-bench/file-based-apps-starters/base/`
- **Base files:** `flat/webapi.cs`, `flat/webapi.run.json`, `flat/webapi.settings.json`
- **Suggested tests:** `swe-bench/file-based-apps-tests/LaunchProfileTests.cs`
- **Test filter:** `FullyQualifiedName~LaunchProfileTests`

### Prompt

```text
I'd like `flat/webapi.cs` to show which launch profile and environment it's running with.

Please add a `/diagnostics/runtime` endpoint that returns the ASP.NET Core environment, the current launch profile, a greeting, and the server addresses. Also add a `verify` launch profile for this app that sets a custom greeting so I can quickly check profile-specific settings.
```

### Expected outcome

- Running with `--launch-profile verify` uses `flat/webapi.run.json`.
- `GET /diagnostics/runtime` returns environment, launch profile, greeting, and URLs.
- The greeting can come from profile/environment configuration, with a sensible default.
- Existing root and OpenAPI behavior is preserved.

## `damianedwards__runfile-fileapps-shell-script`

- **Expected tech:** .NET 10 file-based app; shebang line; LF/no BOM requirements; command-line arguments
- **Starter root:** `swe-bench/file-based-apps-starters/base/`
- **Base files:** none; creates `flat/hello-shell.cs`
- **Suggested tests:** `swe-bench/file-based-apps-tests/ShellExecutableTests.cs`
- **Test filter:** `FullyQualifiedName~ShellExecutableTests`

### Prompt

```text
Can you add a small C# script at `flat/hello-shell.cs` that works nicely as a shell script on macOS/Linux but can still be run with dotnet?

It should greet the name I pass, default to `World`, and use a shebang so I can make it executable later.
```

### Expected outcome

- `flat/hello-shell.cs` starts with `#!/usr/bin/env dotnet`.
- The file uses LF line endings and has no UTF-8 BOM.
- `dotnet run --file flat/hello-shell.cs -- Ada` prints `Hello, Ada!`.

## `damianedwards__runfile-fileapps-build-output`

- **Expected tech:** .NET 10 file-based app; `#:property OutputPath`; `dotnet build`; command-line arguments
- **Starter root:** `swe-bench/file-based-apps-starters/base/`
- **Base files:** none; creates `flat/report.cs`
- **Suggested tests:** `swe-bench/file-based-apps-tests/BuildOutputTests.cs`
- **Test filter:** `FullyQualifiedName~BuildOutputTests`

### Prompt

```text
Please add a tiny report generator at `flat/report.cs`.

Running it should print `report ready`. I also want `dotnet build flat/report.cs` to put build output under `flat/artifacts/report-build` by default so the generated files stay near the script.
```

### Expected outcome

- `flat/report.cs` runs with `dotnet run --file flat/report.cs`.
- The file uses an `OutputPath` property directive.
- `dotnet build flat/report.cs` succeeds and writes output under `flat/artifacts/report-build`.

## `damianedwards__runfile-fileapps-aot-json-publish`

- **Expected tech:** .NET 10 file-based app; Native AOT by default; `System.Text.Json` source generation; `dotnet publish`; custom publish output
- **Starter root:** `swe-bench/file-based-apps-starters/base/`
- **Base files:** none; creates `flat/json-summary.cs`
- **Suggested tests:** `swe-bench/file-based-apps-tests/AotJsonPublishTests.cs`
- **Test filter:** `FullyQualifiedName~AotJsonPublishTests`

### Prompt

```text
Please add a tiny JSON summary tool at `flat/json-summary.cs`.

It should accept a path to a JSON file containing an array of orders like:

[
  { "id": 1, "customer": "Ada", "total": 12.50 },
  { "id": 2, "customer": "Grace", "total": 7.25 }
]

Running it should print the number of orders and the total amount, for example:

`Orders: 2`
`Total: 19.75`

I also want to be able to publish it as a file-based app. Remember that file-based apps publish with native AOT by default, so make the JSON handling publish-friendly.
```

### Expected outcome

- `flat/json-summary.cs` uses `System.Text.Json` source generation with a `JsonSerializerContext`.
- The app does not disable Native AOT.
- `dotnet publish flat/json-summary.cs --output <folder>` succeeds.
- The published app can run against a sample JSON file and print the expected summary.

## `damianedwards__runfile-fileapps-disable-aot-json-publish`

- **Expected tech:** .NET 10 file-based app; `#:property PublishAot=false`; `System.Text.Json` DOM/dynamic-ish workflow; `dotnet publish`; custom publish output
- **Starter root:** `swe-bench/file-based-apps-starters/base/`
- **Base files:** none; creates `flat/json-inspect.cs`
- **Suggested tests:** `swe-bench/file-based-apps-tests/DisableAotJsonPublishTests.cs`
- **Test filter:** `FullyQualifiedName~DisableAotJsonPublishTests`

### Prompt

```text
Please add a quick JSON inspection utility at `flat/json-inspect.cs`.

I want to pass it a JSON file and a property name, and have it print the value of that property from the root object. This is just a flexible local debugging helper, so using the JSON DOM is fine.

Example:

`dotnet run --file flat/json-inspect.cs -- settings.json serviceName`

should print the value of `serviceName`.

Also make sure I can publish it to a custom output folder. Since this utility is intentionally flexible rather than AOT-optimized, configure the file app to publish without native AOT.
```

### Expected outcome

- `flat/json-inspect.cs` includes `#:property PublishAot=false`.
- The app can read a root JSON property by name.
- `dotnet publish flat/json-inspect.cs --output <folder>` succeeds.
- The published app can run against a sample JSON file and print the requested value.

## `damianedwards__runfile-fileapps-aspire-apphost`

- **Expected tech:** .NET 10 file-based app; `#:sdk Aspire.AppHost.Sdk`; non-default SDK directive; app orchestration
- **Starter root:** `swe-bench/file-based-apps-starters/base/`
- **Base files:** `flat/webapi.cs`, `razorapp/razorapp.cs`
- **Suggested tests:** `swe-bench/file-based-apps-tests/AspireAppHostTests.cs`
- **Test filter:** `FullyQualifiedName~AspireAppHostTests`

### Prompt

```text
Can you add an Aspire app host for these samples?

Put it at `flat/apphost.cs`. It should use the Aspire AppHost SDK and add the existing `flat/webapi.cs` and `razorapp/razorapp.cs` file-based apps as resources named `webapi` and `razorapp`. The Razor app should reference and wait for the Web API.
```

### Expected outcome

- `flat/apphost.cs` uses `#:sdk Aspire.AppHost.Sdk`.
- The AppHost adds both existing file-based apps with the expected resource names.
- The Razor app references and waits for the Web API resource.

## `damianedwards__runfile-fileapps-project-ref`

- **Expected tech:** .NET 10 file-based app; `#:project`; class library reference; top-level statements; argument parsing
- **Starter root:** `swe-bench/file-based-apps-starters/base/`
- **Base files:** `classlibproject/samples/hello.cs`, `classlibproject/ClassLib/Greeter.cs`, `classlibproject/ClassLib/ClassLib.csproj`
- **Suggested tests:** `swe-bench/file-based-apps-tests/ProjectRefGreetingTests.cs`
- **Test filter:** `FullyQualifiedName~ProjectRefGreetingTests`

### Prompt

```text
Can you make the `classlibproject` greeting sample a bit more realistic?

I'd like `classlibproject/samples/hello.cs` to support `--name`, `--language`, and `--shout`. Put the reusable greeting formatting in the `ClassLib` project. It should support English, Spanish, and French greetings, and return a clear error for unsupported language codes.
```

### Expected outcome

- The sample still runs with `dotnet run --file classlibproject/samples/hello.cs -- ...`.
- Greeting formatting lives in `ClassLib.Greeter`.
- Supported outputs include `Hello, Ada!`, `Hola, Ada!`, and `Bonjour, Ada!`.
- Invalid language codes exit with code 2 and write `Unsupported language: <code>` to stderr.

## `damianedwards__runfile-fileapps-razor-dashboard`

- **Expected tech:** .NET 10 file-based app; `#:sdk Microsoft.NET.Sdk.Web`; Razor components; Blazor interactivity; static assets
- **Starter root:** `swe-bench/file-based-apps-starters/base/`
- **Base files:** `razorapp/razorapp.cs`, `razorapp/App.razor`, `razorapp/Home.razor`, `razorapp/razorapp.run.json`
- **Suggested tests:** `swe-bench/file-based-apps-tests/RazorDashboardTests.cs`
- **Test filter:** `FullyQualifiedName~RazorDashboardTests`

### Prompt

```text
Can you add a diagnostics page to the Razor app in the `razorapp` folder?

I'd like a `/diagnostics` page that shows the current local time, process id, ASP.NET Core environment, and the entry-point file path/directory for the app. Add a Refresh button that updates the time without navigating away from the page.
```

### Expected outcome

- `/` still renders the existing home page.
- `/diagnostics` renders runtime diagnostics.
- The page includes stable `data-testid` values for tests:
  - `diagnostics-page`
  - `diagnostics-environment`
  - `diagnostics-process-id`
  - `diagnostics-entry-file`
  - `diagnostics-entry-directory`
  - `diagnostics-local-time`
  - `diagnostics-refresh`
- The refresh action updates the local time in place.

## Test asset notes

- `swe-bench/file-based-apps-tests/` contains normal C# test source files. Treat these as the source of truth for expected behavior.
- These tests are scenario-specific. They are not expected to pass as one suite against the current base commit.
- Hidden tests should generally invoke apps with `dotnet run --file <path>.cs -- ...` so the benchmark measures file-based app behavior rather than traditional project behavior.
- It is still useful for hidden tests to assert that agents did not create unnecessary project files for tiny file-based app scenarios, but the prompts intentionally do not over-instruct that behavior.

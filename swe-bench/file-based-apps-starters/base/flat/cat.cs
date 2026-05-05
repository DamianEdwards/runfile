#!/usr/bin/env dotnet

// Properties to enable experimental features for file-based apps
// - include/exclude directives moving to non-experimental in 10.0.300
// - transitive directives experimental status still being debated for 10.0.300
// Note: These could be put in a `Directory.Build.props` file instead if preferred
#:property ExperimentalFileBasedProgramEnableIncludeDirective=true
#:property ExperimentalFileBasedProgramEnableExcludeDirective=true
#:property ExperimentalFileBasedProgramEnableTransitiveDirectives=true

// Include other files in the virtual project, item type is inferred from the file extension
// Note: Using globs here is supported but effectively disables MSBuild caching atm
#:include ./cat.commands.cs

var rootCommand = CatCli.Commands.DefineRootCommand();
var parseResult = rootCommand.Parse(args);
return await parseResult.InvokeAsync();

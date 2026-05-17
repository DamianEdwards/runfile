#!/usr/bin/env dotnet

// Transitive directives are experimental in 10.0.300
// Note: These could be put in a `Directory.Build.props` file instead if preferred
#:property ExperimentalFileBasedProgramEnableTransitiveDirectives=true

// Include other files in the virtual project, item type is inferred from the file extension
// Note: Using globs here is supported but effectively disables MSBuild caching for now
#:include ./cat.commands.cs

var rootCommand = CatCli.Commands.DefineRootCommand();
var parseResult = rootCommand.Parse(args);
return await parseResult.InvokeAsync();

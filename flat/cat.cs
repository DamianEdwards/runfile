#!/usr/bin/env dotnet

// Include other files in the virtual project, item type is inferred from the file extension
// Note: Using globs here is supported but effectively disables MSBuild caching for now
#:include ./cat.commands.cs

var rootCommand = CatCli.Commands.DefineRootCommand();
var parseResult = rootCommand.Parse(args);
return await parseResult.InvokeAsync();

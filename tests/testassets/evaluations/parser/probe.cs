#!/usr/bin/env dotnet
#:property TargetFramework=net10.0
#:property LangVersion=14.0
#:property Nullable=enable
#:property PublishAot=false
#:package System.CommandLine
#:include CandidateAdapter.cs

using System.Text.Json;
using Rafter.Evaluation;

var adapter = new CandidateAdapter { NameValidator = name => name.Length > 0 };
var result = adapter.Evaluate(args);
Console.WriteLine(JsonSerializer.Serialize(result));
foreach (var diagnostic in result.Diagnostics)
{
    Console.Error.WriteLine($"{diagnostic.Category}: {diagnostic.Option ?? "command line"}");
}

return result.ExitCode;

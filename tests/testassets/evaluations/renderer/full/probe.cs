#!/usr/bin/env dotnet
#:property TargetFramework=net10.0
#:property LangVersion=14.0
#:property Nullable=enable
#:property PublishAot=false
#:package Spectre.Console
#:include ../shared/RendererDestination.cs
#:include ../shared/HelpSamples.cs
#:include ../shared/RendererProbe.cs
#:include ../shared/BenchmarkProbe.cs

return await Rafter.Evaluation.RendererProbe.RunAsync(args);

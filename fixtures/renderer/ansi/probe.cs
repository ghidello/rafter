#!/usr/bin/env dotnet
#:property TargetFramework=net10.0
#:property LangVersion=14.0
#:property Nullable=enable
#:property PublishAot=false
#:package Spectre.Console.Ansi
#:include ../shared/BenchmarkProbe.cs
#:include AnsiProbe.cs

return Rafter.Evaluation.AnsiProbe.Run(args);

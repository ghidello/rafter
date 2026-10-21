#!/usr/bin/env dotnet
#:property TargetFramework=net10.0
#:property LangVersion=14.0
#:property Nullable=enable
#:property PublishAot=false
#:include ../shared/BenchmarkProbe.cs
#:include FrameworkProbe.cs

return Rafter.Evaluation.FrameworkProbe.Run(args);

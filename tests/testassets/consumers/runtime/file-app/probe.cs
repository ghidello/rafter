#!/usr/bin/env dotnet
#:project ../../../../../src/Sotsera.Rafter/Sotsera.Rafter.csproj
#:property PublishAot=false
#:property TreatWarningsAsErrors=true
#:include ../Scenario.cs
#:include ../RuntimeCapture.cs

return await RuntimeCapture.RunAsync(args);

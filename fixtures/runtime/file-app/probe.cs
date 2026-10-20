#!/usr/bin/env dotnet
#:project ../../../src/Sotsera.Rafter/Sotsera.Rafter.csproj
#:property PublishAot=false
#:property TreatWarningsAsErrors=true
#:include ../Scenario.cs

return await Scenario.RunAsync(args);

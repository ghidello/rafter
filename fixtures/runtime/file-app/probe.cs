#!/usr/bin/env dotnet
#:project ../../../src/Sotsera.Rafter/Sotsera.Rafter.csproj
#:property PublishAot=false
#:include ../Scenario.cs

return await Scenario.RunAsync(args);

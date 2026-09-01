#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj
#:package Microsoft.Extensions.Configuration.UserSecrets@10.0.0

using Microsoft.Extensions.Configuration;
using Sotsera.Rafter;

IConfigurationRoot configuration = new ConfigurationBuilder()
    .AddUserSecrets(typeof(Program).Assembly, optional: true)
    .Build();

var hasToken = !string.IsNullOrEmpty(configuration["Rafter:ExampleToken"]);

var command = Rafter.Command(Root.Invocation)
    .Description("Use application-owned development configuration.");

var inspect = command.Target("inspect")
    .Description("Run a harmless check when the development token is available.")
    .When(hasToken)
    .Run(context => context.Output.Success("The optional development token is available."));

return await command.RunAsync(inspect, args);

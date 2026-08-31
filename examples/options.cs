#:package Sotsera.Rafter@0.1.0

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Demonstrate command options.");

var project = command.RequiredOption<string>("project")
    .Description("Project or solution to build.");

var configuration = command.Option<BuildConfiguration>("configuration")
    .Description("Build configuration.")
    .Alias('c')
    .Default(BuildConfiguration.Release);

var publish = command.Option<bool>("publish")
    .Description("Create publish output after building.")
    .Default(false);

var token = command.Option<string>("token")
    .Description("Optional service token.")
    .FromEnvironment("RAFTER_EXAMPLE_TOKEN")
    .Sensitive();

var inspect = command.Target("inspect")
    .Description("Print the resolved non-sensitive options.")
    .Run(context =>
    {
        context.Output.Property("project", context.Value(project));
        context.Output.Property("configuration", context.Value(configuration));
        context.Output.Property("publish", context.Value(publish));
        context.Output.Property("hasToken", context.Value(token) is not null);
    });

return await command.RunAsync(inspect, args);

enum BuildConfiguration
{
    Debug,
    Release,
}

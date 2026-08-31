#:package Sotsera.Rafter@0.1.0

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Prepare common filesystem locations safely.");

var output = command.Option<string>("output")
    .Description("Directory receiving generated files.")
    .Default("artifacts/generated");

var prepare = command.Target("prepare")
    .Description("Create required directories and reset generated output.")
    .Run(context =>
    {
        context.FileSystem.EnsureDirectory("artifacts");
        context.FileSystem.EnsureEmptyDirectory(output);
    });

return await command.RunAsync(prepare, args);

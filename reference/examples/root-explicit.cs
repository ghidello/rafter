#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using Sotsera.Rafter;

var command = Rafter.Command(Root.At(".."))
    .Description("Use the parent of the invocation directory as the command root.");

var showRoot = command.Target("show-root")
    .Description("Print the full directory path selected by Root.At(\"..\").")
    .Run(context => context.Output.WriteLine(context.Root));

return await command.RunAsync(showRoot, args);

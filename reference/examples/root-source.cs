#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using Sotsera.Rafter;

var command = Rafter.Command(Root.Source)
    .Description("Use the script's source directory as the command root.");

var showRoot = command.Target("show-root")
    .Description("Print the source directory used by this command.")
    .Run(context => context.Output.WriteLine(context.Root));

return await command.RunAsync(showRoot, args);

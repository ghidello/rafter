#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Use the directory from which this command is invoked as its root.");

var showRoot = command.Target("show-root")
    .Description("Print the invocation directory captured when the command starts.")
    .Run(context => context.Output.WriteLine(context.Root));

return await command.RunAsync(showRoot, args);

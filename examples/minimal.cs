// Syntax draft: the new Rafter library is not implemented yet.
// The default root is the working directory captured when RunAsync starts.
using Sotsera.Rafter;

var command = Rafter.Command("Print a greeting.");

return await command.RunAsync(
    args,
    context => context.WriteLine("Hello from Rafter."));

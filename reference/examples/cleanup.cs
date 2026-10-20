#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using Sotsera.Rafter;

string? sharedFile = null;
string? preparationFile = null;

var command = Rafter.Command(Root.Invocation)
    .Description("Release target resources immediately and shared resources when the command finishes.")
    .Cleanup("Delete the shared input file after all entered work settles.", context =>
    {
        if (sharedFile is not null)
        {
            File.Delete(sharedFile);
            context.Output.WriteLine("Shared resources released.");
        }
    });

var fail = command.Option<bool>("fail")
    .Description("Demonstrate command cleanup after a target fails.")
    .Default(false);

var prepare = command.Target("prepare")
    .Description("Create one temporary file for this target and one shared input for the workflow.")
    .Run(async context =>
    {
        sharedFile = Path.Combine(context.Root, $"rafter-cleanup-shared-{Guid.NewGuid():N}.txt");
        preparationFile = Path.Combine(context.Root, $"rafter-cleanup-target-{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(sharedFile, "prepared inputs", context.CancellationToken);
        await File.WriteAllTextAsync(preparationFile, "temporary preparation", context.CancellationToken);
        context.Output.WriteLine("Inputs prepared.");
    })
    .Cleanup("Delete preparation resources before the next target starts.", context =>
    {
        if (preparationFile is not null)
        {
            File.Delete(preparationFile);
            context.Output.WriteLine("Preparation resources released.");
        }
    });

var finish = command.Target("finish")
    .Description("Read the shared input, optionally fail, and finish the workflow.")
    .DependsOn(prepare)
    .Run(async context =>
    {
        var contents = await File.ReadAllTextAsync(sharedFile!, context.CancellationToken);
        context.Output.WriteLine($"Read: {contents}");

        if (context.Value(fail))
        {
            throw new InvalidOperationException("Requested failure after reading shared inputs.");
        }

        context.Output.WriteLine("Workflow completed.");
    });

return await command.RunAsync(finish, args);

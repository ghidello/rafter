#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Demonstrate typed Git operations.");

var tag = command.RequiredOption<string>("tag")
    .Description("Tag to create.");

var push = command.Option<bool>("push")
    .Description("Push the created tag to origin.")
    .Default(false);

var release = command.Target("release-tag")
    .Description("Create and optionally push a release tag.")
    .Run(async context =>
    {
        var revision = await context.Git
            .RevParse("HEAD")
            .Short()
            .Capture();

        await context.Git
            .Tag(tag)
            .Message($"Release {context.Value(tag)} from {revision.StandardOutput.Trim()}")
            .Run();

        if (context.Value(push))
        {
            await context.Git
                .Push("origin")
                .Tag(tag)
                .Run();
        }
    });

return await command.RunAsync(release, args);

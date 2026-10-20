#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using System.Globalization;
using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Read duration inputs without starting a timer or imposing a deadline.");

var delay = command.Option<TimeSpan>("delay")
    .Description("A nonnegative delay to display.")
    .FromEnvironment("RAFTER_DELAY")
    .Validate("Must be zero or greater.", value => value >= TimeSpan.Zero)
    .Default(TimeSpan.FromSeconds(90));

var offset = command.Option<TimeSpan>("offset")
    .Description("An optional signed offset to display.");

var show = command.Target("show")
    .Description("Print invariant clock values for the selected durations.")
    .Run(context =>
    {
        var delayValue = context.Value(delay);
        var offsetValue = context.Value(offset);
        var offsetText = offsetValue?.ToString("c", CultureInfo.InvariantCulture) ?? "not supplied";

        context.Output.WriteLine($"Delay: {delayValue.ToString("c", CultureInfo.InvariantCulture)}");
        context.Output.WriteLine($"Offset: {offsetText}");
    });

return await command.RunAsync(show, args);

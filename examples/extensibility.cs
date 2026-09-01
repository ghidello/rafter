#:project ../src/Sotsera.Rafter/Sotsera.Rafter.csproj

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Sotsera.Rafter;

var command = Rafter.Command(Root.Invocation)
    .Description("Extend process builders with application-owned conveniences.");

var fixture = command.RequiredOption<string>("fixture")
    .Description("Path to the deterministic process fixture.");

var inspect = command.Target("inspect")
    .Description("Capture and deserialize JSON emitted by a child process.")
    .Run(async context =>
    {
        var payload = await context.Process(fixture)
            .Argument("json")
            .CaptureJson(ProcessJsonContext.Default.FixturePayload);

        context.Output.Property("payload", payload.Value);
    });

return await command.RunAsync(inspect, args);

sealed record FixturePayload(string Value);

[JsonSerializable(typeof(FixturePayload))]
sealed partial class ProcessJsonContext : JsonSerializerContext;

static class ProcessExtensions
{
    public static async Task<T> CaptureJson<T>(
        this ProcessBuilder process,
        JsonTypeInfo<T> typeInfo)
    {
        var result = await process.Capture();
        return JsonSerializer.Deserialize(result.StandardOutput, typeInfo)
            ?? throw new JsonException("The process returned JSON null.");
    }
}

#:property IsPackable=false

using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;

if (args.Length != 3)
{
    throw new ArgumentException("Expected runtime package, symbol package, and Git revision.", nameof(args));
}

using ZipArchive package = ZipFile.OpenRead(args[0]);
using ZipArchive symbols = ZipFile.OpenRead(args[1]);
using MemoryStream assemblyStream = await ReadEntryAsync(package, "lib/net10.0/Sotsera.Rafter.dll").ConfigureAwait(false);
using MemoryStream symbolStream = await ReadEntryAsync(symbols, "lib/net10.0/Sotsera.Rafter.pdb").ConfigureAwait(false);
using PEReader assembly = new(assemblyStream);
using MetadataReaderProvider provider = MetadataReaderProvider.FromPortablePdbStream(symbolStream);
MetadataReader reader = provider.GetMetadataReader();
BlobContentId pdbId = new(reader.DebugMetadataHeader!.Id);
DebugDirectoryEntry codeView = assembly.ReadDebugDirectory().Single(entry => entry.Type == DebugDirectoryEntryType.CodeView);
if (assembly.ReadCodeViewDebugDirectoryData(codeView).Guid != pdbId.Guid || codeView.Stamp != pdbId.Stamp)
{
    throw new InvalidDataException("The symbol package does not match the runtime assembly.");
}

Guid sourceLinkKind = new("cc110556-a091-4d38-9fec-25ab9a351a6a");
CustomDebugInformation sourceLink = reader.CustomDebugInformation
    .Select(reader.GetCustomDebugInformation)
    .SingleOrDefault(info => reader.GetGuid(info.Kind) == sourceLinkKind);
if (sourceLink.Value.IsNil)
{
    throw new InvalidDataException("The packaged PDB contains no Source Link map.");
}

using JsonDocument document = JsonDocument.Parse(reader.GetBlobBytes(sourceLink.Value));
JsonProperty[] mappings = document.RootElement.GetProperty("documents").EnumerateObject().ToArray();
string expectedUrl = $"https://raw.githubusercontent.com/ghidello/rafter/{args[2]}/*";
if (mappings.Length != 1
    || !string.Equals(mappings[0].Value.GetString(), expectedUrl, StringComparison.Ordinal)
    || !mappings[0].Name.EndsWith('*'))
{
    throw new InvalidDataException("Source Link must map the repository to its canonical URL and current Git revision.");
}

string root = mappings[0].Name[..^1];
int mappedSources = reader.Documents
    .Select(handle => reader.GetString(reader.GetDocument(handle).Name))
    .Count(path => path.StartsWith(root + "src/Sotsera.Rafter/", StringComparison.Ordinal)
        || path.StartsWith(root + "src\\Sotsera.Rafter\\", StringComparison.Ordinal));
if (mappedSources == 0)
{
    throw new InvalidDataException("The Source Link map covers no runtime source documents.");
}

Console.WriteLine($"Verified matching packaged symbols and Source Link for {mappedSources} runtime documents.");

static async Task<MemoryStream> ReadEntryAsync(ZipArchive archive, string name)
{
    using Stream source = (archive.GetEntry(name) ?? throw new InvalidDataException($"Missing {name}.")).Open();
    MemoryStream destination = new();
    await source.CopyToAsync(destination).ConfigureAwait(false);
    destination.Position = 0;
    return destination;
}

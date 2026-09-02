using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;

namespace Sotsera.Rafter.Tests;

public sealed class PhaseThreeFormattingAndRedactionTests
{
    [Fact]
    public void EscapesEveryApprovedTextClassDeterministically()
    {
        string value = "\"\\\b\t\n\f\r\0\u200E😀\uD800";
        string expected = "\""
            + "\\\""
            + "\\\\"
            + "\\b"
            + "\\t"
            + "\\n"
            + "\\f"
            + "\\r"
            + "\\u0000"
            + "\\u200E"
            + "😀"
            + "\\uD800"
            + "\"";

        string formatted = ValueFormatter.FormatText(value, truncate: false);

        formatted.Should().Be(expected);
    }

    [Fact]
    public void TruncatesAfterSixtyFourUnicodeElementsWithoutSplittingAScalar()
    {
        string value = string.Concat(Enumerable.Repeat("😀", 64)) + "last";

        string formatted = ValueFormatter.FormatText(value);

        formatted.Should().Be("\"" + string.Concat(Enumerable.Repeat("😀", 64)) + "...\"");
    }

    [Fact]
    public void FormatsApprovedDefaultsInvariantly()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("it-IT");
            Format(12.5d, typeof(double)).Should().Be("12.5");
            Format(12.5m, typeof(decimal)).Should().Be("12.5");
            Format(Guid.Parse("AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE"), typeof(Guid))
                .Should().Be("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
            Format(new DateOnly(2026, 9, 2), typeof(DateOnly)).Should().Be("2026-09-02");
            Format(TimeSpan.FromMinutes(2), typeof(TimeSpan)).Should().Be("00:02:00");
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void RecognizesTheCompleteApprovedFormatterWhitelist()
    {
        (Type aliasedType, object aliasedValue) = CreateAliasedEnum();
        (object Value, Type Type)[] values = [
            ("text", typeof(string)),
            (true, typeof(bool)),
            ('x', typeof(char)),
            ((sbyte)-1, typeof(sbyte)),
            ((byte)1, typeof(byte)),
            ((short)-2, typeof(short)),
            ((ushort)2, typeof(ushort)),
            (-3, typeof(int)),
            (3u, typeof(uint)),
            (-4L, typeof(long)),
            (4UL, typeof(ulong)),
            ((Int128)(-5), typeof(Int128)),
            ((UInt128)5, typeof(UInt128)),
            ((nint)(-6), typeof(nint)),
            ((nuint)6, typeof(nuint)),
            ((Half)1.5f, typeof(Half)),
            (1.5f, typeof(float)),
            (1.5d, typeof(double)),
            (1.5m, typeof(decimal)),
            (Guid.Empty, typeof(Guid)),
            (new DateTime(2026, 9, 2, 1, 2, 3, DateTimeKind.Utc), typeof(DateTime)),
            (new DateTimeOffset(2026, 9, 2, 1, 2, 3, TimeSpan.Zero), typeof(DateTimeOffset)),
            (new DateOnly(2026, 9, 2), typeof(DateOnly)),
            (new TimeOnly(1, 2, 3), typeof(TimeOnly)),
            (TimeSpan.FromSeconds(1), typeof(TimeSpan)),
            (aliasedValue, aliasedType),
        ];

        foreach ((object value, Type type) in values)
        {
            ValueFormatter.TryFormatStable(value, type, out string stable, out string display).Should().BeTrue(type.Name);
            stable.Should().NotBeNull();
            display.Should().NotBeNull();
        }

        Format(aliasedValue, aliasedType).Should().Be("First");
    }

    [Fact]
    public void RejectsNullUndefinedEnumAndUnformattableSensitiveDefaults()
    {
        Command command = NewCommand();
        command.Option<string>("null-value").Description("Null.").Default(null!);
        command.Option<Mode>("enum-value").Description("Enum.").Default((Mode)42);
        command.Option<CustomValue>("custom-value").Description("Custom.").Default(new CustomValue(1)).Sensitive();
        command.Target("entry").Description("Entry.");

        string[] codes = command.FreezeResult.Diagnostics.Select(static diagnostic => diagnostic.Code).ToArray();

        codes.Should().Contain("RAFTER1120");
        codes.Should().Contain("RAFTER1121");
        codes.Should().Contain("RAFTER1122");
    }

    [Fact]
    public void RedactsOverlappingSubstringUnicodeAndMultilinePatterns()
    {
        TextRedactor.Registry registry = new();
        registry.Add("abc");
        registry.Add("bc");
        registry.Add("秘密\nnext");
        TextRedactor redactor = registry.Freeze();

        bool success = redactor.TryRedact("zabc and 秘密\nnext.", out string result);

        success.Should().BeTrue();
        result.Should().Be("z<redacted> and <redacted>.");
    }

    [Fact]
    public void SelectsAnotherMarkerWhenThePreferredMarkerIsSensitive()
    {
        TextRedactor.Registry registry = new();
        registry.Add("<redacted>");
        TextRedactor redactor = registry.Freeze();

        bool success = redactor.TryRedact("value=<redacted>", out string result);

        success.Should().BeTrue();
        result.Should().Be("value=<redacted:1>");
    }

    [Fact]
    public void OneCharacterAndCanonicallyDistinctPatternsKeepExactOrdinalSemantics()
    {
        TextRedactor.Registry registry = new();
        registry.Add(string.Empty);
        registry.Add("a");
        registry.Add("é");
        TextRedactor redactor = registry.Freeze();

        bool success = redactor.TryRedact("banana e\u0301 é", out string result);

        success.Should().BeTrue();
        result.Should().NotContain("a");
        result.Should().Contain("e\u0301");
        result.Should().NotContain("é");
    }

    [Fact]
    public void AdjacentMatchesRemainDistinctWhileOverlappingMatchesMerge()
    {
        TextRedactor.Registry registry = new();
        registry.Add("a");
        TextRedactor redactor = registry.Freeze();

        redactor.TryRedact("aa", out string result).Should().BeTrue();

        result.Should().Be(redactor.Marker + redactor.Marker);
    }

    private static string Format(object value, Type type)
    {
        ValueFormatter.TryFormatStable(value, type, out string stable, out _).Should().BeTrue();
        return stable;
    }

    private static (Type Type, object Value) CreateAliasedEnum()
    {
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("Rafter.AliasedEnum.Tests"),
            AssemblyBuilderAccess.Run);
        ModuleBuilder module = assembly.DefineDynamicModule("Main");
        EnumBuilder builder = module.DefineEnum("AliasedMode", TypeAttributes.Public, typeof(int));
        _ = builder.DefineLiteral("First", 1);
        _ = builder.DefineLiteral("Alias", 1);
        Type type = builder.CreateTypeInfo()!.AsType();
        return (type, Enum.ToObject(type, 1));
    }

    private static Command NewCommand()
        => global::Sotsera.Rafter.Rafter.Command(Root.Invocation).Description("Test command.");

    private enum Mode
    {
        First,
    }

    private readonly record struct CustomValue(int Value) : IParsable<CustomValue>
    {
        public static CustomValue Parse(string value, IFormatProvider? provider) => new(int.Parse(value, provider));

        public static bool TryParse(string? value, IFormatProvider? provider, out CustomValue result)
        {
            bool success = int.TryParse(value, NumberStyles.Integer, provider, out int parsed);
            result = new CustomValue(parsed);
            return success;
        }
    }
}

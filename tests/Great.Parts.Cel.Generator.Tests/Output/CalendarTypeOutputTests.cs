using Great.Parts.Cel.Generator.Tests.Examples;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Great.Parts.Cel.Generator.Tests.Output;

// DateOnly and TimeOnly are members that the generator describes: no diagnostic, the CEL type date or time_of_day, and the conversion
// helper of CelObjectType that the created code calls without a number of the field (as it does for decimal). A Nullable<> member that
// has no value is UnexpectedNull, since CEL has no null of these types. The keys of a dictionary are never dates.
public sealed class CalendarTypeOutputTests
{
    private const string T = "global::Great.Parts.Cel.Types.CelType";

    private static string Source(string member) =>
        GeneratorExample.Preamble
        + "[CelObject(typeof(Holder))]\npublic partial class Ctx : CelTypeContext { }\n"
        + "public class Holder { public long Kept { get; set; } " + member + " }\n";

    [Theory]
    [Trait("Requirement", "F-24")]
    [InlineData("public System.DateOnly Wide { get; set; }", "FromDate(instance.Wide)", T + ".Date")]
    [InlineData("public System.TimeOnly Wide { get; set; }", "FromTimeOfDay(instance.Wide)", T + ".TimeOfDay")]
    [InlineData("public System.DateOnly? Wide { get; set; }", "FromDate(v.Value)", T + ".Date")]
    [InlineData("public System.TimeOnly? Wide { get; set; }", "FromTimeOfDay(v.Value)", T + ".TimeOfDay")]
    [InlineData("public System.Collections.Generic.IReadOnlyList<System.DateOnly> Wide { get; set; } = [];", "FromDate(e0)", T + ".ListOf(" + T + ".Date)")]
    [InlineData("public System.DateOnly[] Wide { get; set; } = [];", "FromDate(e0)", T + ".ListOf(" + T + ".Date)")]
    [InlineData("public System.Collections.Generic.List<System.TimeOnly?> Wide { get; set; } = [];", "FromTimeOfDay(", T + ".ListOf(" + T + ".TimeOfDay)")]
    [InlineData("public System.Collections.Generic.Dictionary<string, System.TimeOnly> Wide { get; set; } = [];", "FromTimeOfDay(e0)", T + ".MapOf(" + T + ".String, " + T + ".TimeOfDay)")]
    [InlineData("public System.Collections.Generic.HashSet<System.DateOnly> Wide { get; set; } = [];", "CelList.CreateSorted<", T + ".ListOf(" + T + ".Date)")]
    public static void Member_IsDescribedAndReadWithItsHelper(string member, string call, string celType)
    {
        GeneratorDriverRunResult result = GeneratorExample.Run(GeneratorExample.Compile(Source(member)));

        Assert.Empty(GeneratorExample.Reported(result));
        string text = GeneratorExample.CreatedText(result);
        Assert.Contains("new(1, \"wide\", " + celType + ")", text, System.StringComparison.Ordinal);
        Assert.Contains(call, text, System.StringComparison.Ordinal);
        Assert.DoesNotContain("GetExcludedFieldReason", text, System.StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Requirement", "F-24")]
    [InlineData("public System.DateOnly? Wide { get; set; }")]
    [InlineData("public System.TimeOnly? Wide { get; set; }")]
    public static void NullableMember_WithoutAValue_IsUnexpectedNull(string member)
    {
        string text = GeneratorExample.CreatedText(GeneratorExample.Run(GeneratorExample.Compile(Source(member))));

        Assert.Contains("HasValue", text, System.StringComparison.Ordinal);
        Assert.Contains("CelErrorCode.UnexpectedNull", text, System.StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Requirement", "F-24")]
    public static void DictionaryWithDateKeys_IsAWarningOfTheKey()
    {
        GeneratorDriverRunResult result = GeneratorExample.Run(GeneratorExample.Compile(Source("public System.Collections.Generic.Dictionary<System.DateOnly, long> Wide { get; set; } = [];")));

        Diagnostic diagnostic = Assert.Single(GeneratorExample.Reported(result));
        Assert.Equal("GPCEL002", diagnostic.Id);
        Assert.Contains("dictionary keys must be", diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture),System.StringComparison.Ordinal);
    }
}

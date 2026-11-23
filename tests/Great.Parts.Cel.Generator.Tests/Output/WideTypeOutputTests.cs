using Great.Parts.Cel.Generator.Tests.Examples;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Great.Parts.Cel.Generator.Tests.Output;

// char and the integers wider than 64 bits are members that the generator describes: no diagnostic, the CEL type of the member, and the
// conversion helper of CelObjectType that the created code calls with the number of the field. DateOnly and TimeOnly have their own
// tests (CalendarTypeOutputTests).
public sealed class WideTypeOutputTests
{
    private const string T = "global::Great.Parts.Cel.Types.CelType";

    private static string Source(string member) =>
        GeneratorExample.Preamble
        + "[CelObject(typeof(Holder))]\npublic partial class Ctx : CelTypeContext { }\n"
        + "public class Holder { public long Kept { get; set; } " + member + " }\n";

    [Theory]
    [Trait("Requirement", "F-27")]
    [InlineData("public char Wide { get; set; }", "FromChar(1, instance.Wide)", T + ".String")]
    [InlineData("public char? Wide { get; set; }", "FromChar(1, v.Value)", T + ".WrapperOf(" + T + ".String)")]
    [InlineData("public System.Int128 Wide { get; set; }", "FromInt128(1, instance.Wide)", T + ".Int")]
    [InlineData("public System.Int128? Wide { get; set; }", "FromInt128(1, v.Value)", T + ".WrapperOf(" + T + ".Int)")]
    [InlineData("public System.UInt128 Wide { get; set; }", "FromUInt128(1, instance.Wide)", T + ".UInt")]
    [InlineData("public System.UInt128? Wide { get; set; }", "FromUInt128(1, v.Value)", T + ".WrapperOf(" + T + ".UInt)")]
    [InlineData("public System.Numerics.BigInteger Wide { get; set; }", "FromBigInteger(1, instance.Wide)", T + ".Int")]
    [InlineData("public System.Numerics.BigInteger? Wide { get; set; }", "FromBigInteger(1, v.Value)", T + ".WrapperOf(" + T + ".Int)")]
    [InlineData("public System.Collections.Generic.IReadOnlyList<char> Wide { get; set; } = [];", "FromChar(1, e0)", T + ".ListOf(" + T + ".String)")]
    [InlineData("public char[] Wide { get; set; } = [];", "FromChar(1, e0)", T + ".ListOf(" + T + ".String)")]
    [InlineData("public System.Collections.Generic.List<System.Int128> Wide { get; set; } = [];", "FromInt128(1, e0)", T + ".ListOf(" + T + ".Int)")]
    [InlineData("public System.Collections.Generic.Dictionary<char, long> Wide { get; set; } = [];", "CelMap.CreateView<char, long>", T + ".MapOf(" + T + ".String, " + T + ".Int)")]
    [InlineData("public System.Collections.Generic.Dictionary<string, System.UInt128> Wide { get; set; } = [];", "FromUInt128(1, e0)", T + ".MapOf(" + T + ".String, " + T + ".UInt)")]
    [InlineData("public System.Collections.Generic.HashSet<char> Wide { get; set; } = [];", "CelList.CreateSorted<char>", T + ".ListOf(" + T + ".String)")]
    public static void Member_IsDescribedAndReadWithItsHelper(string member, string call, string celType)
    {
        GeneratorDriverRunResult result = GeneratorExample.Run(GeneratorExample.Compile(Source(member)));

        Assert.Empty(GeneratorExample.Reported(result));
        string text = GeneratorExample.CreatedText(result);
        Assert.Contains("new(1, \"wide\", " + celType + ")", text, System.StringComparison.Ordinal);
        Assert.Contains(call, text, System.StringComparison.Ordinal);
        Assert.DoesNotContain("GetExcludedFieldReason", text, System.StringComparison.Ordinal);
    }
}

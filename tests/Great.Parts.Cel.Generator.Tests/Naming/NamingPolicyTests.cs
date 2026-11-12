using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using Great.Parts.Cel.Generator.Tests.Examples;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Generator.Tests.Naming;

// The policies CamelCase and SnakeCaseLower are written in the generator, which cannot reference System.Text.Json of its own runtime
// ("Names" of the page of the generator). The test compares the field names that the created code gives with the names that
// JsonNamingPolicy gives for the same C# names, over a corpus of acronyms, digits, underscores, a single letter, leading underscores and
// names in the upper case entirely. A name where the two differ fails the test and the generator is changed, not the corpus. Each name is
// the only member of a class of its own, so two names that a policy makes equal (ID and Id) are not GPCEL003.
public sealed class NamingPolicyTests
{
    private static readonly string[] Corpus =
    [
        "A", "a", "AB", "ab", "Ab", "aB", "ABc", "ABC", "aBC",
        "ID", "Id", "id", "Name", "name", "ALLUPPER", "already", "TotalAmount", "totalAmount",
        "URLPath", "XMLHttp", "HTTPServer", "SomeURL", "IOStream", "OKButton", "HTMLElement", "UserID42", "X1Y2", "A1", "A1b", "A1B",
        "Order2Line", "Order2", "Order22Line", "Line2", "ABC123", "abc123", "Abc123Def",
        "_Leading", "__Two", "_a", "_A", "_AB", "Trailing_", "Trailing__", "With_Underscore", "With_underscore", "A_B", "a_B", "Ab_Cd", "AB_CD", "_",
        "iOS", "eBay", "pH", "mRNA", "DNAStrand", "ThisIsALongNameWithManyWords", "THISISALONGNAMEINUPPERCASE", "Z", "zZ", "ZZ",
    ];

    [Fact]
    [Trait("Requirement", "F-2")]
    public void CamelCase_GivesTheNamesOfJsonNamingPolicyCamelCase() =>
        AssertSameNames("", JsonNamingPolicy.CamelCase);

    [Fact]
    [Trait("Requirement", "F-2")]
    public void SnakeCaseLower_GivesTheNamesOfJsonNamingPolicySnakeCaseLower() =>
        AssertSameNames("[CelTypeContextOptions(NamingPolicy = CelNamingPolicy.SnakeCaseLower)]", JsonNamingPolicy.SnakeCaseLower);

    [Fact]
    [Trait("Requirement", "F-2")]
    public void AsIs_GivesTheCSharpNames() =>
        AssertNames("[CelTypeContextOptions(NamingPolicy = CelNamingPolicy.AsIs)]", static name => name);

    private static void AssertSameNames(string options, JsonNamingPolicy policy) =>
        AssertNames(options, name => policy.ConvertName(name));

    private static void AssertNames(string options, Func<string, string> expected)
    {
        string attributes = string.Concat(Enumerable.Range(0, Corpus.Length).Select(i => "[CelObject(typeof(T" + i + "))]\n"));
        var text = new StringBuilder("using Great.Parts.Cel.Values;\nnamespace Acme;\n");
        text.Append(attributes).Append(options).Append("\npublic partial class Ctx : CelTypeContext { }\n");
        for (int i = 0; i < Corpus.Length; i++)
        {
            text.Append("public class T").Append(i).Append(" { public long ").Append(Corpus[i]).Append(" { get; set; } }\n");
        }

        using var loaded = new GeneratorExample.Loaded(text.ToString());
        CelTypeProvider provider = loaded.Provider("Acme.Ctx");

        string[] actual = [.. Enumerable.Range(0, Corpus.Length).Select(i => Assert.Single(provider.ObjectTypes.Single(type => type.Name == "Acme.T" + i).Fields).Name)];
        string[] want = [.. Corpus.Select(expected)];
        Assert.Equal(want, actual);
    }
}

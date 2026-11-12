using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Cel.Expr;
using Great.Parts.Cel.Eng.Difftest.Compare;
using Great.Parts.Cel.Eng.Difftest.Profile;
using Great.Parts.Cel.Eng.Difftest.Sides;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Eng.Tests.Support;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Eng.Tests.Difftest;

// The profile of the extension sets (docs/design/eng-difftest.md, "Profile of the extension sets"): the cases of the extension
// profile of fixed seeds, the strings of the sets, the printer and the minimizer on the nodes of the sets.
public sealed class ExtensionGeneratorTests
{
    // The cases per seed of the tests that read every case.
    private const int Cases = 1000;

    // The cases per seed of the tests of forms that are rarer.
    private const int ManyCases = Cases * 10;

    // The most cases per seed that the search for the rarest forms reads; it ends as soon as every form has occurred.
    private const int SearchLimit = 150_000;

    // The names that the macros of the sets bind: each is read only inside its macro.
    private static readonly string[] MacroNames = ["v", "w", "i", "e", "j", "t", "bn1", "bn2"];

    // The methods of the set network: an abstract node is their receiver or, for the containment tests, their argument.
    private static readonly string[] NetworkMethods =
    [
        "family", "isLoopback", "isUnspecified", "isLinkLocalUnicast", "isLinkLocalMulticast", "isGlobalUnicast", "prefixLength", "isMask",
        "masked", "ip", "containsIP", "containsCIDR",
    ];

    // ---- The generator ----

    [Fact]
    public void EveryCaseOfTheExtensionProfile_TypeChecksExceptTheOnesThatFaultOnPurpose()
    {
        CelEnvironment environment = GeneratorTests.Environment();

        foreach (GeneratedCase generated in Sample().Where(generated => !HasFault(generated) && !MatchesGeneratorTests.HasRefusedLiteralPattern(generated)))
        {
            CelCompileResult<CelValue> compiled = environment.Compile<CelValue>(generated.Text);

            Assert.True(compiled.IsSuccess, generated.Text + ": " + string.Join("; ", compiled.Errors.Select(error => error.Message)));
        }
    }

    [Fact]
    public void CallsOfMathThatTheMacroRefuses_AreRefusedAtParsingAndFormatCallsWithAFault_HaveConstantArgumentsAndAreRefusedWithTheCodeOfAnInvalidFormat()
    {
        CelEnvironment environment = GeneratorTests.Environment();
        List<GeneratedCase> refused = [.. Sample(ManyCases).Where(generated => Calls(generated).Any(call => call.Fault == "math-refused")).Take(40)];

        Assert.NotEmpty(refused);
        Assert.All(refused, generated => Assert.False(environment.Compile<CelValue>(generated.Text).IsSuccess, generated.Text));
        List<Call> faulty = [.. Sample().SelectMany(Calls).Where(call => call.Fault == "format")];

        Assert.NotEmpty(faulty);
        Assert.All(faulty, call => Assert.True(call is { Function: "format", Args: [StringLiteral, ListLiteral] }, ExprPrinter.Print(call)));

        // The library refuses each of them when the expression is compiled, with the code of an invalid format string; a call without
        // the fault compiles, which the test of the type check above holds.
        Assert.All(
            faulty,
            call =>
            {
                CelCompileResult<CelValue> compiled = environment.Compile<CelValue>(ExprPrinter.Print(call));

                Assert.False(compiled.IsSuccess, ExprPrinter.Print(call));
                Assert.Contains(compiled.Errors, error => error.Code == CelErrorCode.InvalidFormatString);
            });
    }

    // Every overload of every set occurs: the generator names the overload that a call aims at. The search ends when all have occurred.
    [Fact]
    public void EveryOverloadOfTheSets_Occurs()
    {
        HashSet<string> ids = [.. ProfileTables.ExtensionOverloads.Select(overload => overload.Id)];
        HashSet<string> missing = [.. ids];
        HashSet<string> seen = [];

        for (ulong n = 0; n < SearchLimit && missing.Count > 0; n++)
        {
            foreach (ulong seed in GeneratorTests.Seeds)
            {
                foreach (Call call in Calls(CaseGenerator.ExtensionCase(seed, n)).Where(call => call.Overload is not null))
                {
                    _ = seen.Add(call.Overload!);
                    _ = missing.Remove(call.Overload!);
                }
            }
        }

        Assert.Empty(missing.Order(StringComparer.Ordinal));
        Assert.All(seen, id => Assert.Contains(id, ids));
        string[] sets = ["encoders", "lists", "math", "network", "sets", "strings"];
        Assert.Equal(sets, ProfileTables.ExtensionOverloads.Select(overload => overload.Set!).Distinct().Order(StringComparer.Ordinal));
    }

    // Every macro and every special form of the sets occurs. The search ends when all have occurred.
    [Fact]
    public void EveryMacroAndSpecialFormOfTheSets_Occurs()
    {
        Dictionary<string, Func<Expr, bool>> forms = new()
        {
            ["sortBy"] = node => node is Comprehension { Macro: ProfileTables.SortByMacro },
            ["cel.bind"] = node => node is Comprehension { Macro: ProfileTables.BindMacro },
            ["cel.bind that reads its name"] = node => node is Comprehension { Macro: ProfileTables.BindMacro } bind
                && GeneratorTests.Nodes(bind.Arguments[0]).OfType<Ident>().Any(ident => ident.Name == bind.Variable),
            ["cel.bind that hides a variable"] = node => node is Comprehension { Macro: ProfileTables.BindMacro } bind
                && ProfileTables.HidableVariables.Contains(bind.Variable),
            ["cel.bind with a name of its own"] = node => node is Comprehension { Macro: ProfileTables.BindMacro } bind
                && ProfileTables.BindingNames.Contains(bind.Variable),
            ["transformList with a filter"] = node => node is Comprehension { Macro: "transformList", Arguments.Count: 2 },
            ["transformList"] = node => node is Comprehension { Macro: "transformList", Arguments.Count: 1 },
            ["transformMap"] = node => node is Comprehension { Macro: "transformMap" },
            ["transformMapEntry"] = node => node is Comprehension { Macro: "transformMapEntry" },
            ["format with a variable list"] = node => node is Call { Function: "format", Args: [_, Ident { Name: "lx" }] },
            ["format with a string in dyn()"] = node => node is Call { Function: "format", Args: [Call { Function: ProfileTables.Dyn }, _] },
            ["format of constants with a fault"] = node => node is Call { Function: "format", Fault: "format" },
            ["format of constants without a fault"] = node => node is Call { Function: "format", Fault: null, Args: [StringLiteral, ListLiteral] },
            ["math.greatest the macro refuses"] = node => node is Call { Function: "math.greatest", Fault: "math-refused" },
            ["math.least the macro refuses"] = node => node is Call { Function: "math.least", Fault: "math-refused" },
            ["math with numbers of mixed kinds"] = node => node is Call { Function: "math.greatest" or "math.least", Fault: null, Overload: { } id }
                && id.Split('_').Length >= 4 && id.Split('_')[^1] != id.Split('_')[^2] && !id.Contains("list", StringComparison.Ordinal),
            ["size of a large lists.range"] = node => node is Call { Function: ProfileTables.Size, Args: [Call { Function: "lists.range", Args: [IntLiteral { Value: 1_000_000 or 1_000_001 }] }] },
            ["lists.range"] = node => node is Call { Function: "lists.range", Args: [IntLiteral { Value: 1000 }] },
            ["size of a two-variable map"] = node => node is Call { Function: ProfileTables.Size, Args: [Comprehension { Macro: "transformMap" or "transformMapEntry" }] },
            ["index of a two-variable map"] = node => node is Call { Function: ProfileTables.Index, Args: [Comprehension { Macro: "transformMap" or "transformMapEntry" }, _] },
            ["in a two-variable map"] = node => node is Call { Function: ProfileTables.In, Args: [_, Comprehension { Macro: "transformMap" or "transformMapEntry" }] },
            ["== on a two-variable map"] = node => node is Call { Function: ProfileTables.Equal, Args: [Comprehension { Macro: "transformMap" or "transformMapEntry" }, _] },
            ["type(x) == net.IP"] = node => node is Call { Function: ProfileTables.Equal, Args: [Call { Function: ProfileTables.TypeOf }, Ident { Name: "net.IP" }] },
            ["type(x) == net.CIDR"] = node => node is Call { Function: ProfileTables.Equal, Args: [Call { Function: ProfileTables.TypeOf }, Ident { Name: "net.CIDR" }] },
            ["== on two addresses"] = node => node is Call { Function: ProfileTables.Equal, Args: [Call { Function: "ip" }, Call { Function: "ip" }] },
            ["!= on two prefixes"] = node => node is Call { Function: ProfileTables.NotEqual, Args: [Call { Function: "cidr" }, Call { Function: "cidr" }] },
            ["the address of a prefix"] = node => node is Call { Function: "ip", Receiver: true },
            ["a masked prefix"] = node => node is Call { Function: "masked", Receiver: true },
            ["dyn() around a text of the set network"] = node => node is Call { Function: "ip" or "cidr", Args: [Call { Function: ProfileTables.Dyn }] },
        };
        foreach (string function in new[] { "all", "exists", "existsOne", "exists_one" })
        {
            forms["two-variable " + function] = node => node is Comprehension { SecondVariable: not null } macro && macro.Macro == function;
        }

        HashSet<string> missing = [.. forms.Keys];
        for (ulong n = 0; n < SearchLimit && missing.Count > 0; n++)
        {
            foreach (ulong seed in GeneratorTests.Seeds)
            {
                foreach (Expr node in GeneratorTests.Nodes(CaseGenerator.ExtensionCase(seed, n).Expression))
                {
                    _ = missing.RemoveWhere(name => forms[name](node));
                }
            }
        }

        Assert.Empty(missing.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void AbstractNodes_StandOnlyWhereTheOracleIsNotAskedForThem()
    {
        List<GeneratedCase> cases = [.. Sample()];

        Assert.All(cases, generated => Assert.Null(ProfileTables.AbstractResult(generated.Expression)));
        Assert.All(cases, generated => AssertConfined(generated.Expression, null));
        Assert.Contains(cases, generated => GeneratorTests.Nodes(generated.Expression).Any(node => ProfileTables.AbstractResult(node) == ProfileType.IpType));
        Assert.Contains(cases, generated => GeneratorTests.Nodes(generated.Expression).Any(node => ProfileTables.AbstractResult(node) == ProfileType.CidrType));
    }

    [Fact]
    public void ListsThatSortAndSortByOrder_HaveAtMost12Elements()
    {
        List<Expr> receivers = [.. Sample(Cases * 3).SelectMany(generated => GeneratorTests.Nodes(generated.Expression)).SelectMany(SortReceivers)];

        Assert.NotEmpty(receivers);
        Assert.Contains(receivers, receiver => receiver is Ident);
        Assert.Contains(receivers, receiver => receiver is ListLiteral);
        Assert.All(receivers, receiver => Assert.True(
            receiver is ListLiteral { Elements.Count: <= 3 } or Ident { Name: "li" or "ls" },
            ExprPrinter.Print(receiver)));
    }

    [Fact]
    public void ElementsOfAFormatListThatTheOracleReadsAsTheLibraryDoes_HaveNoInvalidBytesAndNoVariableOfDyn()
    {
        List<Call> formats = [.. Sample(ManyCases).SelectMany(Calls).Where(call => call.Function == "format" && call.Args[1] is ListLiteral)];
        UTF8Encoding strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        Assert.NotEmpty(formats);
        foreach (Call call in formats)
        {
            List<Expr> nodes = [.. GeneratorTests.Nodes(call.Args[1])];

            Assert.DoesNotContain(nodes.OfType<Ident>(), ident => ident.Name is "x1" or "x2" or "y1");
            Assert.All(nodes.OfType<BytesLiteral>(), bytes => Assert.NotNull(strict.GetString(bytes.Value.ToArray())));
            Assert.InRange(((ListLiteral)call.Args[1]).Elements.Count, 0, ProfileTables.MaxElements);
        }

        Assert.Contains(formats, call => GeneratorTests.Nodes(call.Args[1]).Any(node => node is BytesLiteral));
        Assert.Contains(formats, call => GeneratorTests.Nodes(call.Args[1]).Any(node => node is MapLiteral { Entries.Count: 2 }));
        Assert.Contains(formats, call => GeneratorTests.Nodes(call.Args[1]).Any(node => node is DoubleLiteral));
    }

    // Case 1496201 of seed 4188587866: `'...%.0s '.format(lx)` with the first element of lx the bytes dd c7 3d 7f. cel-go gives the Go
    // string with the raw bytes, which has no form in cel.expr.Value and ended the oracle with exit code 1; the library gives U+FFFD
    // (the known difference string_format). The profile does not meet the difference: the binding of lx is valid UTF-8.
    [Fact]
    public void CaseThatFormatsTheVariableLx_BindsItToBytesOfValidUtf8()
    {
        UTF8Encoding strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        List<GeneratedCase> formatting =
        [
            .. Sample(ManyCases).Where(generated => Calls(generated).Any(call => call.Function == "format" && call.Args[1] is Ident { Name: "lx" })),
            CaseGenerator.ExtensionCase(4188587866, 1496201),
        ];

        Assert.True(formatting.Count > 1);
        Assert.All(formatting, generated => Assert.True(
            !generated.Bindings.TryGetValue("lx", out Value? lx) || BytesIn(lx).All(bytes => IsValidUtf8(strict, bytes)),
            generated.Text));
        Assert.Contains(formatting, generated => generated.Bindings.TryGetValue("lx", out Value? lx) && BytesIn(lx).Any());
    }

    // The bytes of a value: an invalid sequence is replaced byte by byte with '?', a valid one stays, in a list and a map too.
    [Fact]
    public void WithValidUtf8Bytes_ReplacesEachByteOfAnInvalidSequenceAndKeepsTheRest()
    {
        static Value Bytes(params byte[] bytes) => new() { BytesValue = Google.Protobuf.ByteString.CopyFrom(bytes) };

        // dd c7 3d 7f: two invalid lead bytes; e2 82: a sequence cut short; e2 82 ac and f0 9f 98 80: valid sequences; ff after a valid one.
        Assert.Equal(Bytes(0x3F, 0x3F, 0x3D, 0x7F), ProfileValues.WithValidUtf8Bytes(Bytes(0xDD, 0xC7, 0x3D, 0x7F)));
        Assert.Equal(Bytes(0x3F, 0x3F), ProfileValues.WithValidUtf8Bytes(Bytes(0xE2, 0x82)));
        Assert.Equal(Bytes(0xE2, 0x82, 0xAC, 0xF0, 0x9F, 0x98, 0x80), ProfileValues.WithValidUtf8Bytes(Bytes(0xE2, 0x82, 0xAC, 0xF0, 0x9F, 0x98, 0x80)));
        Assert.Equal(Bytes(0xE2, 0x82, 0xAC, 0x3F), ProfileValues.WithValidUtf8Bytes(Bytes(0xE2, 0x82, 0xAC, 0xFF)));
        Assert.Equal(Bytes(), ProfileValues.WithValidUtf8Bytes(Bytes()));

        Value list = new() { ListValue = new ListValue { Values = { Bytes(0xFF), new Value { Int64Value = 1 } } } };
        Value map = new()
        {
            MapValue = new MapValue
            {
                Entries = { new MapValue.Types.Entry { Key = new Value { StringValue = "k" }, Value = Bytes(0xC0, 0x41) } },
            },
        };
        Assert.Equal(new[] { new byte[] { 0x3F } }, BytesIn(ProfileValues.WithValidUtf8Bytes(list)));
        Assert.Equal(new[] { new byte[] { 0x3F, 0x41 } }, BytesIn(ProfileValues.WithValidUtf8Bytes(map)));
        Assert.Equal(new Value { Int64Value = 1 }, ProfileValues.WithValidUtf8Bytes(new Value { Int64Value = 1 }));
    }

    // The 26 cases of seed 277579806 that the run on 108b881 reported: cel.bind(bn1, (list), …), cel.bind(bn2, null_type, x2) and the
    // like. cel-go plans a name whose static type is type as a type name, so the program fails with 'reference to undefined type: bn1'
    // wherever the library gives the value; a value of that type is bound in dyn(), as the range of a macro is.
    [Fact]
    public void CelBind_NeverBindsAValueOfTheStaticTypeTypeWithoutDyn()
    {
        ulong[] reported =
        [
            404, 707, 1259, 2147, 2309, 2474, 4532, 5660, 6881, 12770, 15638, 15788, 17033, 17168, 19568, 19793, 25793, 39491, 59471, 64121, 87398,
            115532, 123506, 132041, 188510, 444167,
        ];
        List<GeneratedCase> cases = [.. Sample(ManyCases), .. reported.Select(n => CaseGenerator.ExtensionCase(277579806, n))];
        List<Comprehension> binds =
        [
            .. cases.SelectMany(generated => GeneratorTests.Nodes(generated.Expression)).OfType<Comprehension>().Where(macro => macro.Macro == ProfileTables.BindMacro),
        ];

        static bool IsTypeValue(Expr value) =>
            (value is Ident { Name: { } name } && (ProfileTables.TypeIdentifiers.Contains(name) || ProfileTables.AbstractTypeIdentifiers.Contains(name)))
            || value is Call { Function: ProfileTables.TypeOf };

        Assert.NotEmpty(binds);
        Assert.All(binds, bind => Assert.False(IsTypeValue(bind.Range), ExprPrinter.Print(bind)));
        Assert.Contains(binds, bind => bind.Range is Call { Function: ProfileTables.Dyn, Args: [{ } value] } && IsTypeValue(value));
    }

    [Fact]
    public void CasesOfTheExtensionProfile_KeepTheLimitsAndEachMacroVariableIsReadInsideItsMacro()
    {
        List<GeneratedCase> cases = [.. Sample()];

        Assert.All(cases, generated => Assert.InRange(Depth(generated.Expression), 1, ProfileTables.MaxDepth));
        Assert.All(cases, generated => Assert.InRange(GeneratorTests.Nodes(generated.Expression).Count(), 1, ProfileTables.MaxNodes));
        Assert.All(cases, generated => Assert.InRange(MacroNesting(generated.Expression), 0, ProfileTables.MaxMacroNesting));
        Assert.All(cases, generated => Assert.All(GeneratorTests.Nodes(generated.Expression), node => Assert.InRange(Elements(node), 0, ProfileTables.MaxElements)));
        Assert.All(cases, generated => AssertScoped(generated.Expression, []));
        Assert.Equal(ProfileTables.MaxDepth, cases.Max(generated => Depth(generated.Expression)));
        Assert.Equal(ProfileTables.MaxMacroNesting, cases.Max(generated => MacroNesting(generated.Expression)));
    }

    [Fact]
    public void CaseN_DependsOnTheSeedAndNAlone_AndEveryThirdCaseIsOfTheExtensionProfile()
    {
        Assert.Equal(CaseGenerator.ExtensionCase(20261104, 17).Text, CaseGenerator.ExtensionCase(20261104, 17).Text);
        Assert.Equal(CaseGenerator.ExtensionCase(20261104, 17).Text, CaseGenerator.Case(20261104, 17).Text);
        Assert.Equal(CaseGenerator.StandardCase(20261104, 18).Text, CaseGenerator.Case(20261104, 18).Text);
        Assert.Equal([2UL, 5UL, 8UL], Enumerable.Range(0, 9).Select(n => (ulong)n).Where(CaseGenerator.IsExtensionCase));
        Assert.Contains(
            Enumerable.Range(0, 30).Select(n => (ulong)n),
            n => CaseGenerator.ExtensionCase(1, n).Text != CaseGenerator.StandardCase(1, n).Text);
    }

    // ---- The strings of the sets ----

    [Fact]
    public void TextsForIpAndCidr_AreAddressesAndPrefixesOfEveryFormThatTheSetListsValidAndNot()
    {
        List<string> addresses = [.. Enumerable.Range(0, 4000).Select(n => ExtensionStrings.IpText(CaseGenerator.CaseState(1, (ulong)n)))];
        List<string> prefixes = [.. Enumerable.Range(0, 4000).Select(n => ExtensionStrings.CidrText(CaseGenerator.CaseState(2, (ulong)n)))];

        foreach (string form in new[] { "1.2.3.4", "01.2.3.4", "1.2.3.256", "1.2.3.4.", "0x1.2.3.4", " 1.2.3.4", "::", "::1.2.3.4", "1::2::3", "12345::1", "[::1]", "::ffff:1.2.3.4", "fe80::1%eth0", "2001:0db8:0:0:0:0:0:1", "2001:DB8::1", "1.2.3.\u0664" })
        {
            Assert.Contains(form, addresses);
        }

        foreach (string form in new[] { "10.0.0.1/8", "10.0.0.0/33", "10.0.0.0/08", "10.0.0.0/+8", "10.0.0.0/", "10.0.0.0", "::ffff:1.2.3.4/96", "::1/128", "::1/129" })
        {
            Assert.Contains(form, prefixes);
        }

        // Valid and not valid: the library, through the profile, tells both for each kind.
        byte[] manifest = File.ReadAllBytes(Path.Combine(RepositoryRoot.Find(), ProfileTables.ManifestPath));
        Assert.True(LibrarySide.TryBuild(manifest, Path.Combine(RepositoryRoot.Find(), ProfileTables.ZoneInfoPath), out LibrarySide? side, out string problem), problem);
        (bool Valid, bool Invalid) addressKinds = Verdicts(side!, "isIP(s1)", addresses);
        (bool Valid, bool Invalid) prefixKinds = Verdicts(side!, "isCIDR(s1)", prefixes);

        Assert.True(addressKinds.Valid && addressKinds.Invalid);
        Assert.True(prefixKinds.Valid && prefixKinds.Invalid);
    }

    [Fact]
    public void PoolOfTheStringFunctions_HoldsTheStringsThatTestIndexesCodePointsAndEscapes()
    {
        UTF8Encoding strict = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        string[] pool = ExtensionStrings.Pool;

        Assert.All(pool, text => Assert.True(strict.GetBytes(text).Length >= 0));
        Assert.Contains(string.Empty, pool);
        Assert.Contains("abc", pool);
        Assert.Contains("\u00e9", pool);
        Assert.Contains("\U0001f600", pool);
        // A character above U+FFFF that sorts after one below U+E000 by code points and before it by UTF-16 units.
        Assert.Contains("\ue000", pool);
        Assert.Contains("\U00010000", pool);
        Assert.Contains(pool, text => text.Contains('\u00a0', StringComparison.Ordinal) && text.Contains('\u3000', StringComparison.Ordinal));
        Assert.Contains(pool, text => text.Contains("aaa", StringComparison.Ordinal));
        foreach (char escaped in new[] { '\a', '\b', '\f', '\n', '\r', '\t', '\v', '\\', '"' })
        {
            Assert.Contains(pool, text => text.Contains(escaped, StringComparison.Ordinal));
        }
    }

    // ---- The printer and the minimizer on the nodes of the sets ----

    [Fact]
    public void NodesOfTheSets_PrintAsCalls_Macros_AndBindings()
    {
        Expr one = Literal(1);
        Expr two = Literal(2);
        Expr positive = new Call(ProfileTables.Greater, [new Ident("e"), Literal(0)]);
        Expr abs = new Call("math.abs", [one]);
        Expr bind = new Comprehension(ProfileTables.BindMacro, one, "bn1", [new Call(ProfileTables.Add, [new Ident("bn1"), two])]);
        Expr all = new Comprehension("all", new ListLiteral([one], TrailingComma: false), "i", [positive]) { SecondVariable = "e" };
        Expr sortBy = new Comprehension(ProfileTables.SortByMacro, new Ident("li"), "v", [new Call(ProfileTables.Negate, [new Ident("v")])]);
        Expr ip = new Call("ip", [new StringLiteral("1.2.3.4", Plain("1.2.3.4"))]);
        Expr isCanonical = new Call("ip.isCanonical", [new StringLiteral("::1", Plain("::1"))]);
        Expr member = new Call("family", [ip]) { Receiver = true };
        Expr quote = new Call("strings.quote", [new StringLiteral("a", Plain("a"))]);

        Assert.Equal("math.abs(1)", ExprPrinter.Print(abs));
        Assert.Equal("cel.bind(bn1, 1, bn1 + 2)", ExprPrinter.Print(bind));
        Assert.Equal("[1].all(i, e, e > 0)", ExprPrinter.Print(all));
        Assert.Equal("li.sortBy(v, -v)", ExprPrinter.Print(sortBy));
        Assert.Equal("ip('1.2.3.4').family()", ExprPrinter.Print(member));
        Assert.Equal("ip.isCanonical('::1')", ExprPrinter.Print(isCanonical));
        Assert.Equal("strings.quote('a')", ExprPrinter.Print(quote));
        Assert.Equal("1 + cel.bind(bn1, 1, bn1)", ExprPrinter.Print(new Call(ProfileTables.Add, [one, new Comprehension(ProfileTables.BindMacro, one, "bn1", [new Ident("bn1")])])));
    }

    [Fact]
    public void NodeOfAnAbstractType_IsAskedForAsItsStringAndKeepsTheNameOfItsType()
    {
        Expr ip = new Call("ip", [new StringLiteral("1.2.3.4", Plain("1.2.3.4"))]);
        Expr cidr = new Call("cidr", [new StringLiteral("10.0.0.0/8", Plain("10.0.0.0/8"))]);
        Expr masked = new Call("masked", [cidr]) { Receiver = true };
        Expr address = new Call("ip", [cidr]) { Receiver = true };
        Expr containment = new Call("containsIP", [cidr, ip]) { Receiver = true };

        Assert.Equal(ProfileType.IpType, ProfileTables.AbstractResult(ip));
        Assert.Equal(ProfileType.CidrType, ProfileTables.AbstractResult(cidr));
        Assert.Equal(ProfileType.CidrType, ProfileTables.AbstractResult(masked));
        Assert.Equal(ProfileType.IpType, ProfileTables.AbstractResult(address));
        Assert.Null(ProfileTables.AbstractResult(containment));
        Assert.Null(ProfileTables.AbstractResult(Literal(1)));
        Assert.Equal("string(ip('1.2.3.4'))", new GeneratedCase(ip, new Dictionary<string, Value>()).Text);
        Assert.Equal("string(cidr('10.0.0.0/8').masked())", new GeneratedCase(masked, new Dictionary<string, Value>()).Text);
        Assert.Equal("cidr('10.0.0.0/8').containsIP(ip('1.2.3.4'))", new GeneratedCase(containment, new Dictionary<string, Value>()).Text);
    }

    // The instances of a body: cel.bind puts the value in place of the name; of a two-variable macro, a body that reads neither
    // variable is itself an instance, otherwise there is an instance for each element with both variables replaced at once, then each
    // closed node of the body.
    [Fact]
    public void BodiesOfCelBindAndOfTwoVariableMacros_AreInstancesAsTheMinimizerTakesThem()
    {
        Expr value = Literal(7);
        Expr bind = new Comprehension(ProfileTables.BindMacro, value, "bn1", [new Call(ProfileTables.Greater, [new Ident("bn1"), Literal(0)])]);
        Expr reads = new Call(ProfileTables.LogicalAnd, [new Call(ProfileTables.Greater, [new Ident("e"), Literal(0)]), new BoolLiteral(true)]);
        Expr independent = new Call(ProfileTables.Greater, [Literal(1), Literal(0)]);
        Expr range = new ListLiteral([Literal(1)], TrailingComma: false);

        Assert.Equal(["(7) > 0", "0"], Printed(bind));
        Assert.Equal(["(1) > 0 && true", "0", "true"], Printed(new Comprehension("all", range, "i", [reads]) { SecondVariable = "e" }));
        Assert.Equal(["1 > 0"], Printed(new Comprehension("exists", range, "i", [independent]) { SecondVariable = "e" }));
        // The inner macro declares j and e and reads the t of the outer one: the t of the inner macro is replaced, its own e is not, and
        // the outer macro has the range of the inner one as its only closed node.
        Expr inner = new Comprehension("all", range, "j", [new Call(ProfileTables.Greater, [new Ident("e"), new Ident("t")])]) { SecondVariable = "e" };
        Assert.Equal(["[1].all(j, e, e > (1))", "[1]"], Printed(new Comprehension("all", range, "i", [inner]) { SecondVariable = "t" }));
        // The index and the element of a list, in dyn() each in dyn(); the key and the value of a map, and a string literal once more in dyn().
        Expr body = new Call(ProfileTables.Add, [new Ident("i"), new Ident("e")]);
        Expr strings = new MapLiteral([new MapEntry(new StringLiteral("k", Plain("k")), new StringLiteral("v", Plain("v")))], TrailingComma: false);
        Assert.Equal(["(0) + (1)"], Printed(new Comprehension("all", range, "i", [body]) { SecondVariable = "e" }));
        Assert.Equal(["dyn(0) + dyn(1)"], Printed(new Comprehension("all", new Call(ProfileTables.Dyn, [range]), "i", [body]) { SecondVariable = "e" }));
        Assert.Equal(["('k') + ('v')", "dyn('k') + dyn('v')"], Printed(new Comprehension("all", strings, "i", [body]) { SecondVariable = "e" }));
    }

    // ---- Helpers ----

    private static IEnumerable<GeneratedCase> Sample(int cases = Cases)
    {
        foreach (ulong seed in GeneratorTests.Seeds)
        {
            for (ulong n = 0; n < (ulong)cases; n++)
            {
                yield return CaseGenerator.ExtensionCase(seed, n);
            }
        }
    }

    // The list that sort or sortBy orders.
    private static IEnumerable<Expr> SortReceivers(Expr node)
    {
        if (node is Call { Function: "sort", Receiver: true } call)
        {
            yield return call.Args[0];
        }
        else if (node is Comprehension { Macro: ProfileTables.SortByMacro } macro)
        {
            yield return macro.Range;
        }
    }

    private static IEnumerable<Call> Calls(GeneratedCase generated) => GeneratorTests.Nodes(generated.Expression).OfType<Call>();

    private static bool HasFault(GeneratedCase generated) => Calls(generated).Any(call => call.Fault is not null);

    // The bytes values of a value, inside a list or a map too.
    private static IEnumerable<byte[]> BytesIn(Value value) => value.KindCase switch
    {
        Value.KindOneofCase.BytesValue => [value.BytesValue.ToByteArray()],
        Value.KindOneofCase.ListValue => value.ListValue.Values.SelectMany(BytesIn),
        Value.KindOneofCase.MapValue => value.MapValue.Entries.SelectMany(entry => BytesIn(entry.Key).Concat(BytesIn(entry.Value))),
        _ => [],
    };

    private static bool IsValidUtf8(UTF8Encoding strict, byte[] bytes)
    {
        try
        {
            _ = strict.GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    private static IEnumerable<string> Printed(Expr macro) => Minimizer.BodyInstances(macro, _ => null).Select(ExprPrinter.Print);

    private static IntLiteral Literal(long value) => new(value, IntegerForm.DecimalDigits);

    private static TextForm Plain(string text) => new(Quote.Apostrophe, null, [.. text.EnumerateRunes().Select(_ => Escape.Literal)]);

    private static (bool Valid, bool Invalid) Verdicts(LibrarySide side, string expression, List<string> texts)
    {
        bool valid = false;
        bool invalid = false;
        foreach (string text in texts.Distinct())
        {
            SideOutcome outcome = side.Evaluate(expression, new Dictionary<string, Value> { ["s1"] = new Value { StringValue = text } });
            Assert.False(outcome.IsError, expression + " of '" + text + "'");
            valid |= outcome.Result!.BoolValue;
            invalid |= !outcome.Result.BoolValue;
        }

        return (valid, invalid);
    }

    // Every child of a node, the arguments of a macro included: a walk of its own.
    private static IEnumerable<Expr> Children(Expr expr) => expr switch
    {
        Comprehension macro => [macro.Range, .. macro.Arguments],
        Select select => [select.Operand],
        PresenceTest test => [test.Operand],
        Call call => call.Args,
        ListLiteral list => list.Elements,
        MapLiteral map => map.Entries.SelectMany(entry => new[] { entry.Key, entry.Value }),
        _ => [],
    };

    private static int Depth(Expr expr) => 1 + Children(expr).Select(Depth).DefaultIfEmpty(0).Max();

    private static int MacroNesting(Expr expr) => (expr is Comprehension ? 1 : 0) + Children(expr).Select(MacroNesting).DefaultIfEmpty(0).Max();

    private static int Elements(Expr expr) => expr switch
    {
        ListLiteral list => list.Elements.Count,
        MapLiteral map => map.Entries.Count,
        _ => 0,
    };

    // A node of an abstract type is the argument of string, ==, != or type, or a receiver or an argument of a method of the set.
    private static void AssertConfined(Expr node, Expr? parent)
    {
        if (ProfileTables.AbstractResult(node) is not null)
        {
            Assert.True(
                parent is Call call
                    && (call.Function is ProfileTables.StringConversion or ProfileTables.Equal or ProfileTables.NotEqual or ProfileTables.TypeOf
                        || (call.Receiver && NetworkMethods.Contains(call.Function))),
                parent is null ? "an abstract root" : ExprPrinter.Print(parent));
        }

        foreach (Expr child in Children(node))
        {
            AssertConfined(child, node);
        }
    }

    // Each name of a macro is read only inside a macro that declares it.
    private static void AssertScoped(Expr expr, List<string> scope)
    {
        switch (expr)
        {
            case Ident ident when MacroNames.Contains(ident.Name):
                Assert.Contains(ident.Name, scope);
                break;
            case Comprehension macro:
                if (MacroNames.Contains(macro.Variable))
                {
                    Assert.DoesNotContain(macro.Variable, scope);
                }

                AssertScoped(macro.Range, scope);
                List<string> inner = [.. scope, macro.Variable];
                if (macro.SecondVariable is { } second)
                {
                    inner.Add(second);
                }

                foreach (Expr argument in macro.Arguments)
                {
                    AssertScoped(argument, inner);
                }

                break;
            default:
                foreach (Expr child in Children(expr))
                {
                    AssertScoped(child, scope);
                }

                break;
        }
    }
}

using System;
using System.Linq;
using System.Threading;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Examples.Configuration;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The set formats, version 1: six functions that tell whether a string has a format. Each row is an expression and the bool that it
// gives, or the code of the error and the stage; no function reports an error for a string of another format. A function is a method of
// string, so the receiver is the text. The costs are those of the page of the costs of the set: a call is 1 unit and the receiver adds
// ⌈L/10⌉ for its length.
[Trait("Requirement", "F-28")]
[Trait("Requirement", "R-12")]
public sealed class FormatsExtensionTests
{
    private const string Label63 = "abc012345678901234567890123456789012345678901234567890123456789";
    private const string Label64 = "abcd012345678901234567890123456789012345678901234567890123456789";

    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Formats(1))
        .Build()
        .GetEnvironmentOrThrow();

    [Theory]
    [InlineData("'foo@example.com'.isEmail()", true)]
    [InlineData("'foo+x@example.com'.isEmail()", true)]
    [InlineData("'foo@example'.isEmail()", true)]
    [InlineData("'example.com'.isEmail()", false)]
    [InlineData("'\U000000B5@example.com'.isEmail()", false)]
    [InlineData("'@example.com'.isEmail()", false)]
    [InlineData("''.isEmail()", false)]
    [InlineData("' foo@example.com'.isEmail()", false)]
    [InlineData("'foo@example.com '.isEmail()", false)]
    public static void IsEmail_IsTheHtmlDefinition(string expression, bool expected)
    {
        Assert.Equal(expected, IsTrue(expression));
    }

    [Theory]
    [InlineData("'example.com'.isHostname()", true)]
    [InlineData("'A.ISI.EDU'.isHostname()", true)]
    [InlineData("'foo-bar.com'.isHostname()", true)]
    [InlineData("'a-b.a--b'.isHostname()", true)]
    [InlineData("'a.'.isHostname()", true)]
    [InlineData("'com1'.isHostname()", true)]
    [InlineData("'" + Label63 + ".com'.isHostname()", true)]
    [InlineData("'" + Label64 + ".com'.isHostname()", false)]
    [InlineData("''.isHostname()", false)]
    [InlineData("'.a'.isHostname()", false)]
    [InlineData("'..'.isHostname()", false)]
    [InlineData("'a..b'.isHostname()", false)]
    [InlineData("'-a'.isHostname()", false)]
    [InlineData("'a-'.isHostname()", false)]
    [InlineData("'a.1'.isHostname()", false)]
    [InlineData("'foo_bar.com'.isHostname()", false)]
    [InlineData("'\U00004F60\U0000597D.com'.isHostname()", false)]
    [InlineData("'\U00000130'.isHostname()", false)]
    [InlineData("' example.com'.isHostname()", false)]
    public static void IsHostname_IsLabelsOfLettersDigitsAndHyphens(string expression, bool expected)
    {
        Assert.Equal(expected, IsTrue(expression));
    }

    [Theory]
    [InlineData("'127.0.0.1/16'.isIpPrefix()", true)]
    [InlineData("'::1/64'.isIpPrefix()", true)]
    [InlineData("'127.0.0.1/16'.isIpPrefix(0)", true)]
    [InlineData("'::1/64'.isIpPrefix(0)", true)]
    [InlineData("'127.0.0.1/16'.isIpPrefix(4)", true)]
    [InlineData("'::1/64'.isIpPrefix(6)", true)]
    [InlineData("'192.168.5.21/16'.isIpPrefix(4, false)", true)]
    [InlineData("'192.168.0.0/16'.isIpPrefix(4, true)", true)]
    [InlineData("'2001:0DB8:ABCD:0012::0/64'.isIpPrefix(6, true)", true)]
    [InlineData("'192.168.5.21/16'.isIpPrefix(4, true)", false)]
    [InlineData("'192.168.5.21/16'.isIpPrefix(true)", false)]
    [InlineData("'127.0.0.1/16'.isIpPrefix(6)", false)]
    [InlineData("'::1/64'.isIpPrefix(4)", false)]
    [InlineData("'127.0.0.1/16'.isIpPrefix(5)", false)]
    [InlineData("'127.0.0.1'.isIpPrefix()", false)]
    [InlineData("'127.0.0.1/33'.isIpPrefix()", false)]
    [InlineData("'::1/129'.isIpPrefix()", false)]
    [InlineData("'::1%en1/64'.isIpPrefix()", false)]
    [InlineData("'::1/64%en1'.isIpPrefix()", false)]
    [InlineData("' 127.0.0.1/16'.isIpPrefix()", false)]
    [InlineData("'127.0.0.1/16 '.isIpPrefix()", false)]
    public static void IsIpPrefix_IsAnAddressAndALength(string expression, bool expected)
    {
        Assert.Equal(expected, IsTrue(expression));
    }

    [Theory]
    [InlineData("'example.com'.isHostAndPort(false)", true)]
    [InlineData("'example.com:8080'.isHostAndPort(true)", true)]
    [InlineData("'a.'.isHostAndPort(false)", true)]
    [InlineData("'127.0.0.1:65535'.isHostAndPort(true)", true)]
    [InlineData("'[::1]:80'.isHostAndPort(true)", true)]
    [InlineData("'[::1]'.isHostAndPort(false)", true)]
    [InlineData("'[::1%foo]'.isHostAndPort(false)", true)]
    [InlineData("'example.com'.isHostAndPort(true)", false)]
    [InlineData("'127.0.0.1:65536'.isHostAndPort(true)", false)]
    [InlineData("'192.168.0.1:'.isHostAndPort(false)", false)]
    [InlineData("'[::1%]'.isHostAndPort(false)", false)]
    [InlineData("'[127.0.0.1]'.isHostAndPort(false)", false)]
    [InlineData("''.isHostAndPort(false)", false)]
    [InlineData("' example.com'.isHostAndPort(false)", false)]
    public static void IsHostAndPort_IsAHostAndAPort(string expression, bool expected)
    {
        Assert.Equal(expected, IsTrue(expression));
    }

    [Theory]
    [InlineData("'https://example.com'.isUri()", true)]
    [InlineData("'https://example.com/foo/bar?baz=quux#frag'.isUri()", true)]
    [InlineData("'https://joe@example.com/foo'.isUri()", true)]
    [InlineData("'ftp://example.com'.isUri()", true)]
    [InlineData("''.isUri()", false)]
    [InlineData("' '.isUri()", false)]
    [InlineData("'./'.isUri()", false)]
    [InlineData("'//example.com/foo'.isUri()", false)]
    [InlineData("'https://example.com '.isUri()", false)]
    [InlineData("'./foo'.isUriRef()", true)]
    [InlineData("'./foo/bar?baz=quux#frag'.isUriRef()", true)]
    [InlineData("''.isUriRef()", true)]
    [InlineData("'//host'.isUriRef()", true)]
    [InlineData("'//host/'.isUriRef()", true)]
    [InlineData("' '.isUriRef()", false)]
    [InlineData("':'.isUriRef()", false)]
    [InlineData("'1foo://example.com'.isUriRef()", false)]
    [InlineData("'//host/%x'.isUriRef()", false)]
    public static void IsUriAndIsUriRef_AreRfc3986(string expression, bool expected)
    {
        Assert.Equal(expected, IsTrue(expression));
    }

    [Fact]
    public static void WithoutTheSet_TheFunctionIsUndeclared()
    {
        CelEnvironment without = new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();

        CelCompileResult<bool> result = without.Compile<bool>("'a@b.c'.isEmail()");

        Assert.False(result.IsSuccess);
        Assert.Equal(CelErrorCode.UndeclaredReference, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public static void VersionThatTheSetDoesNotHave_IsUnknownExtensionVersion()
    {
        CelExtension extension = CelExtensions.Formats(2);
        CelBuildResult result = new CelEnvironmentBuilder(CelStandardLibrary.V1).AddExtension(extension).Build();

        Assert.Equal(("formats", 2u), (extension.Name, extension.Version));
        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.UnknownExtensionVersion, error.Code);
        Assert.Equal("extension 'formats' has no version 2; available versions: 1", error.Message);
    }

    [Fact]
    public static void FunctionOfTheApplicationWithTheSignatureOfAFunctionOfTheSet_IsOverlappingOverloads()
    {
        CelBuildResult result = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddExtension(CelExtensions.Formats(1))
            .AddMethod<string, bool>("isEmail", static text => text.Length > 0)
            .Build();

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.OverlappingOverloads, error.Code);
        Assert.Contains("application", error.Message, StringComparison.Ordinal);
        Assert.Contains("extension formats v1", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public static void SameSetAddedAgain_IsOneSet()
    {
        Assert.Same(CelExtensions.Formats(1), CelExtensions.Formats(1));
        Assert.True(new CelEnvironmentBuilder(CelStandardLibrary.V1).AddExtension(CelExtensions.Formats(1)).AddExtension(CelExtensions.Formats(1)).Build().IsSuccess);
    }

    [Theory]
    [InlineData("'a@b.c'.isEmail()")]
    [InlineData("isCIDR('10.0.0.0/8')")]
    [InlineData("'10.0.0.0/8'.isIpPrefix(4, true)")]
    [InlineData("'a,b'.split(',').size() == 2")]
    public static void SetsOfOtherNames_AreInOneEnvironmentWithTheSet(string expression)
    {
        CelEnvironment together = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddExtension(CelExtensions.Formats(1))
            .AddExtension(CelExtensions.Strings(5))
            .AddExtension(CelExtensions.Network(1))
            .Build()
            .GetEnvironmentOrThrow();

        Assert.True(together.Compile<bool>(expression).GetProgramOrThrow().Evaluate(new CelBindings(), cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    [Fact]
    public static void IsIpPrefixAndIsCidr_AgreeOnTheHostBits()
    {
        CelEnvironment together = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddExtension(CelExtensions.Formats(1))
            .AddExtension(CelExtensions.Network(1))
            .Build()
            .GetEnvironmentOrThrow();

        foreach ((string expression, bool expected) in new[]
        {
            ("'10.1.2.3/8'.isIpPrefix()", true),
            ("isCIDR('10.1.2.3/8')", true),
            ("'10.1.2.3/8'.isIpPrefix(true)", false),
            ("cidr('10.1.2.3/8').isMask()", false),
        })
        {
            bool actual = together.Compile<bool>(expression).GetProgramOrThrow().Evaluate(new CelBindings(), cancellationToken: CancellationToken.None).GetValueOrThrow();

            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public static void NameOfTheSetInADescription_IsNotTheNameOfASet()
    {
        JsonExample.AssertError(
            """{"extensions": [{"name": |"formats", "version": "1"}]}""",
            mentions: "'formats' is not the name of an extension set of the library");
    }

    [Theory]
    [InlineData("dyn(1).isEmail()")]
    [InlineData("dyn(true).isHostname()")]
    [InlineData("dyn(1).isUri()")]
    [InlineData("dyn(1).isUriRef()")]
    [InlineData("dyn(1).isIpPrefix()")]
    [InlineData("'1.2.3.4/8'.isIpPrefix(dyn('4'))")]
    [InlineData("'1.2.3.4/8'.isIpPrefix(dyn(4), dyn(1))")]
    [InlineData("'a'.isHostAndPort(dyn(1))")]
    public static void ReceiverOrArgumentOfAnotherKind_IsEvalNoMatchingOverload(string expression)
    {
        CelEvalResult<CelValue> result = Environment.Compile<CelValue>(expression).GetProgramOrThrow().Evaluate(new CelBindings(), cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(CelErrorCode.EvalNoMatchingOverload, Assert.Single(result.Errors).Code);
    }

    // The nine overloads, each with the cost of the page: a call is 1 unit and the receiver adds ⌈L/10⌉; a constant costs 0 and a
    // variable read 1.
    [Theory]
    [InlineData("string_is_email", "'a@b.c'.isEmail()", 2)]
    [InlineData("string_is_hostname", "'example.com'.isHostname()", 3)]
    [InlineData("string_is_host_and_port_bool", "'example.com:8080'.isHostAndPort(true)", 3)]
    [InlineData("string_is_ip_prefix", "'10.0.0.0/8'.isIpPrefix()", 2)]
    [InlineData("string_is_ip_prefix_int", "'10.0.0.0/8'.isIpPrefix(4)", 2)]
    [InlineData("string_is_ip_prefix_bool", "'10.0.0.0/8'.isIpPrefix(true)", 2)]
    [InlineData("string_is_ip_prefix_int_bool", "'10.0.0.0/8'.isIpPrefix(4, true)", 2)]
    [InlineData("string_is_uri", "'https://example.com/foo/bar?baz=quux#frag'.isUri()", 6)]
    [InlineData("string_is_uri_ref", "''.isUriRef()", 1)]
    public static void EveryOverload_HasItsCost(string id, string expression, ulong cost)
    {
        Assert.Contains(id, Environment.Functions.SelectMany(function => function.Overloads).Select(overload => overload.OverloadId));

        CelEvalResult<CelValue> result = Environment.Compile<CelValue>(expression).GetProgramOrThrow()
            .Evaluate(new CelBindings(), new CelEvalOptions { CostLimit = 1000 }, CancellationToken.None);

        Assert.True(result.IsSuccess, expression);
        Assert.Equal(cost, result.Cost);
    }

    [Fact]
    public static void TheSet_HasNineOverloadsAndTheirDescriptions()
    {
        string[] names = ["isEmail", "isHostname", "isHostAndPort", "isIpPrefix", "isUri", "isUriRef"];
        CelOverloadDecl[] overloads = [.. Environment.Functions.Where(function => names.Contains(function.Name, StringComparer.Ordinal)).SelectMany(function => function.Overloads)];

        Assert.Equal(9, overloads.Length);
        Assert.All(overloads, overload => Assert.False(string.IsNullOrEmpty(overload.Description)));
    }

    private static bool IsTrue(string expression) =>
        Environment.Compile<bool>(expression).GetProgramOrThrow().Evaluate(new CelBindings(), cancellationToken: CancellationToken.None).GetValueOrThrow();
}

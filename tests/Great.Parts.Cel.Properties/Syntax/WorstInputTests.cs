using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using CsCheck;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Xunit;

namespace Great.Parts.Cel.Properties.Syntax;

// The time of parsing the worst inputs (docs/design/tests.md, "Properties of the parser"): each input is at most 65 536 UTF-8
// bytes; it is parsed once to warm up and once under a stopwatch. A time over 1 s fails the test only on the reference machine,
// a standard GitHub-hosted Linux x64 runner; elsewhere the time is only written to the output of the test.
public sealed class WorstInputTests
{
    private const int LengthLimit = 65536;
    private const double TimeLimit = 1;
    private const ulong GarbageSeed = 20261105;

    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();

    // The variables that GitHub Actions sets on every runner, with their values on a standard GitHub-hosted Linux x64 runner.
    private static readonly (string Name, string Value)[] ReferenceMachine =
        [("GITHUB_ACTIONS", "true"), ("RUNNER_OS", "Linux"), ("RUNNER_ARCH", "X64"), ("RUNNER_ENVIRONMENT", "github-hosted")];

    [Theory]
    [Trait("Requirement", "R-2")]
    [InlineData("parentheses 249")]
    [InlineData("lists 249")]
    [InlineData("maps 249")]
    [InlineData("calls 249")]
    [InlineData("indexes 249")]
    [InlineData("conditionals 249")]
    [InlineData("relations 124")]
    [InlineData("additions 124")]
    [InlineData("multiplications 124")]
    [InlineData("syntax tree 250")]
    [InlineData("chain ||")]
    [InlineData("chain &&")]
    [InlineData("chain <")]
    [InlineData("chain +")]
    [InlineData("chain *")]
    [InlineData("syntax errors")]
    [InlineData("garbage")]
    public static void WorstInput_IsParsedWithinOneSecond(string input)
    {
        string text = Input(input);
        Assert.True(Encoding.UTF8.GetByteCount(text) <= LengthLimit, $"{input}: {Encoding.UTF8.GetByteCount(text)} bytes");

        _ = Environment.Parse(text);
        var stopwatch = Stopwatch.StartNew();
        ParseResult result = Environment.Parse(text);
        double seconds = stopwatch.Elapsed.TotalSeconds;

        Assert.DoesNotContain(result.Errors, error => error.Code == CelErrorCode.InternalError);
        TestContext.Current.TestOutputHelper?.WriteLine(Time(input, seconds));
        if (Verdict(input, seconds, IsReferenceMachine(System.Environment.GetEnvironmentVariable)) is { } failure)
        {
            Assert.Fail(failure);
        }
    }

    [Theory]
    [InlineData(1.42, true, "chain +: 1.420 s > 1 s")]
    [InlineData(1.42, false, null)]
    [InlineData(1.0, true, null)]
    [InlineData(0.25, true, null)]
    public static void Verdict_FailsOnlyOverOneSecondOnTheReferenceMachine(double seconds, bool referenceMachine, string? failure)
    {
        Assert.Equal(failure, Verdict("chain +", seconds, referenceMachine));
    }

    [Theory]
    [InlineData("true", "Linux", "X64", "github-hosted", true)]
    [InlineData(null, "Linux", "X64", "github-hosted", false)]
    [InlineData("true", "macOS", "ARM64", "github-hosted", false)]
    [InlineData("true", "Linux", "ARM64", "github-hosted", false)]
    [InlineData("true", "Linux", "X64", "self-hosted", false)]
    [InlineData(null, null, null, null, false)]
    public static void ReferenceMachine_IsAStandardGitHubHostedLinuxX64Runner(
        string? actions, string? operatingSystem, string? architecture, string? environment, bool expected)
    {
        var variables = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["GITHUB_ACTIONS"] = actions,
            ["RUNNER_OS"] = operatingSystem,
            ["RUNNER_ARCH"] = architecture,
            ["RUNNER_ENVIRONMENT"] = environment,
        };

        Assert.Equal(expected, IsReferenceMachine(name => variables.GetValueOrDefault(name)));
    }

    // The failure of a time over 1 s on the reference machine, or null.
    private static string? Verdict(string input, double seconds, bool referenceMachine) =>
        referenceMachine && seconds > TimeLimit ? $"{Time(input, seconds)} > 1 s" : null;

    private static string Time(string input, double seconds) =>
        string.Create(CultureInfo.InvariantCulture, $"{input}: {seconds:0.000} s");

    private static bool IsReferenceMachine(Func<string, string?> variable) =>
        ReferenceMachine.All(expected => string.Equals(variable(expected.Name), expected.Value, StringComparison.Ordinal));

    // The deepest nesting allowed for each kind of nesting, chains of each binary level filling 64 KB, a text with more than 100
    // syntax errors, and 64 KB of garbage from a fixed seed.
    private static string Input(string input) => input switch
    {
        "parentheses 249" => Nested("(", "x", ")", 249),
        "lists 249" => Nested("[", "1", "]", 249),
        "maps 249" => Nested("{1: ", "2", "}", 249),
        "calls 249" => Nested("f(", "x", ")", 249),
        "indexes 249" => Nested("x[", "0", "]", 249),
        "conditionals 249" => Nested("c ? a : ", "z", "", 249),
        "relations 124" => Nested("a < (", "b", ")", 124),
        "additions 124" => Nested("x + (", "x", ")", 124),
        "multiplications 124" => Nested("x * (", "x", ")", 124),
        "syntax tree 250" => "x" + Repeat(".f", 250),
        "syntax errors" => Filled("f(a b", ", a b", ")"),
        "garbage" => Garbage(),
        _ => Filled("x", $" {input["chain ".Length..]} x", ""),
    };

    private static string Nested(string open, string inner, string close, int count) => Repeat(open, count) + inner + Repeat(close, count);

    // The head and as many units as fit into 65 536 bytes with the tail.
    private static string Filled(string head, string unit, string tail) =>
        head + Repeat(unit, (LengthLimit - head.Length - tail.Length) / unit.Length) + tail;

    // Printable ASCII characters, so that every character is one byte.
    private static string Garbage()
    {
        const uint Printable = '~' - ' ' + 1;
        PCG random = new(0, GarbageSeed);
        return string.Concat(Enumerable.Range(0, LengthLimit).Select(_ => (char)(' ' + random.Next(Printable))));
    }

    private static string Repeat(string unit, int count) => string.Concat(Enumerable.Repeat(unit, count));
}

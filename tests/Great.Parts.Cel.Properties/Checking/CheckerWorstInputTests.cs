using System.Diagnostics;
using System.Text;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Properties.Generators;
using Great.Parts.Cel.Properties.Syntax;
using Xunit;

namespace Great.Parts.Cel.Properties.Checking;

// The time of parsing and checking the worst inputs (docs/design/tests.md, "Properties of the parser and the checker"): each
// input is at most 65 536 UTF-8 bytes and parses; it is parsed and checked once to warm up and once under a stopwatch. A time
// over 1 s fails the test only on the reference machine, as for parsing alone.
public sealed class CheckerWorstInputTests
{
    private const int Segments = 250;

    // A container of 250 segments a.a.….a: every prefix of a selection chain of names a meets every candidate of the container.
    private static readonly CelEnvironment InContainer =
        TypedExpressions.Declare(new CelEnvironmentBuilder(CelStandardLibrary.V1).UseContainer(Chain(Segments)))
            .Build()
            .GetEnvironmentOrThrow();

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
    [InlineData("list of dyn sums")]
    [InlineData("list of empty lists")]
    [InlineData("list of mixed literals")]
    [InlineData("list of mismatches")]
    [InlineData("sum of 250 empty lists")]
    [InlineData("nested comprehensions")]
    [InlineData("selection chain in a container")]
    public static void WorstInput_IsParsedAndCheckedWithinOneSecond(string input)
    {
        CelEnvironment environment = input == "selection chain in a container" ? InContainer : TypedExpressions.Environment;
        string text = Input(input);
        Assert.True(Encoding.UTF8.GetByteCount(text) <= WorstInputTests.LengthLimit, $"{input}: {Encoding.UTF8.GetByteCount(text)} bytes");
        Assert.True(environment.Parse(text).IsSuccess, $"{input}: does not parse");

        _ = ParseAndCheck(environment, text);
        var stopwatch = Stopwatch.StartNew();
        CheckResult result = ParseAndCheck(environment, text);
        double seconds = stopwatch.Elapsed.TotalSeconds;

        Assert.DoesNotContain(result.Errors, error => error.Code == CelErrorCode.InternalError);
        TestContext.Current.TestOutputHelper?.WriteLine(WorstInputTests.Time(input, seconds));
        if (WorstInputTests.Verdict(input, seconds, WorstInputTests.IsReferenceMachine(System.Environment.GetEnvironmentVariable)) is { } failure)
        {
            Assert.Fail(failure);
        }
    }

    private static CheckResult ParseAndCheck(CelEnvironment environment, string text) =>
        environment.Check(environment.Parse(text).Expression!);

    // The inputs of the parser that parse (the chains of <, + and * fill 64 KB past the depth of the syntax tree and do not), list literals filling 64 KB whose elements make the checker work hardest, a chain of
    // 250 additions of empty lists, comprehensions nested as deep as the parser allows, and a selection chain in a container.
    private static string Input(string input) => input switch
    {
        "list of dyn sums" => FilledList("x1 + x1"),
        "list of empty lists" => FilledList("[]"),
        "list of mixed literals" => FilledList("1, 1u, 1.0, 'a'"),
        "list of mismatches" => FilledList("1 + 'a'"),
        "sum of 250 empty lists" => "[]" + WorstInputTests.Repeat(" + []", Segments),
        "nested comprehensions" => DeepestComprehensions(),
        "selection chain in a container" => Chain(Segments),
        _ => WorstInputTests.Input(input),
    };

    // A list literal of as many elements as fit into 65 536 bytes.
    private static string FilledList(string element)
    {
        string unit = element + ", ";
        return "[" + WorstInputTests.Repeat(unit, (WorstInputTests.LengthLimit - 2 - element.Length) / unit.Length) + element + "]";
    }

    // [1].all(v, [1].all(v, … true)) with as many levels as parse: each level nests at least one call deeper, so no more
    // than 250 levels parse.
    private static string DeepestComprehensions()
    {
        for (int depth = Segments; depth > 0; depth--)
        {
            string text = WorstInputTests.Repeat("[1].all(v, ", depth) + "true" + WorstInputTests.Repeat(")", depth);
            if (TypedExpressions.Environment.Parse(text).IsSuccess)
            {
                return text;
            }
        }

        return "true";
    }

    private static string Chain(int names) => "a" + WorstInputTests.Repeat(".a", names - 1);
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Policy.Yaml;
using Xunit;
using static Great.Parts.Cel.Policy.Tests.YamlTestHelpers;

namespace Great.Parts.Cel.Policy.Tests;

// A document of a policy is an untrusted input: any text gives a result and never an exception, the depth of the document costs no
// stack, a document that stands for an enormous policy is stopped, and a document of one megabyte is read in seconds.
[Trait("Requirement", "R-13")]
public sealed class PolicyUntrustedTests
{
    private const string Corpus = """
        name: corpus
        description: |
          A policy for the tests.
        imports:
          - name: a.b.T
        rule:
          variables:
            - name: x
              expression: >
                request.size +
                1
            - name: y
              expression: "variables.x * 2"
          match:
            - condition: "variables.y > 10"
              output: "'big'"
              explanation: 'it is big'
            - condition: variables.y > 5
              rule:
                id: nested
                match:
                  - output: !txt |
                      'medium'
            - output: "'small'"
        """;

    private const string TextLimitMessage = "the strings of the policy are longer than four times the document";

    // The time limit of the tests of the budget for text and of the document of one megabyte guards a hang or a quadratic growth; the
    // 2 s of the requirement are checked by CompileTimingTests.
    private static readonly TimeSpan GuardTime = TimeSpan.FromSeconds(10);

    private static readonly string[] Pieces = [":", "-", "?", "[", "]", "{", "}", ",", "&a", "*a", "!t", "|", ">", "'", "\"", "%", "#", "\n", "\t", " ", "\\", "---", "..."];

    [Fact]
    public void CorruptedDocuments_GiveAResultWithoutAnException()
    {
        var random = new TestRandom(98);
        for (int i = 0; i < 3000; i++)
        {
            string text = Corrupt(random, Corpus);

            CelPolicyParseResult result = CelPolicy.Parse(text, "p.yaml");

            Assert.Equal(result.IsSuccess, result.Errors.IsEmpty);
            Assert.All(result.Errors, error => Assert.Equal(CelErrorStage.Parse, error.Stage));
            Assert.All(result.Errors, error => Assert.Equal("p.yaml", Location(error).SourceName));
        }
    }

    [Fact]
    public void CorruptedDocuments_GiveTheSameNodesTwice()
    {
        var random = new TestRandom(99);
        for (int i = 0; i < 500; i++)
        {
            string text = Corrupt(random, Corpus);

            YamlReadResult first = YamlReader.Read(text);
            YamlReadResult second = YamlReader.Read(text);

            Assert.Equal(first.IsError, second.IsError);
            Assert.Equal(first.ErrorMessage, second.ErrorMessage);
            Assert.Equal(first.ErrorOffset, second.ErrorOffset);
        }
    }

    [Fact]
    public void TheCorpus_IsAPolicy()
    {
        CelPolicy policy = CelPolicy.Parse(Corpus, "p.yaml").GetPolicyOrThrow();

        Assert.Equal("corpus", policy.Name);
        Assert.Equal(2, policy.Document.Rule!.Variables.Count);
        Assert.Equal(3, policy.Document.Rule.Matches.Count);
    }

    [Theory]
    [InlineData("[")]
    [InlineData("{")]
    [InlineData("- ")]
    [InlineData("{a: ")]
    [InlineData("[{a: ")]
    public static void DeepNesting_IsAnErrorWithoutOverflowingTheStack(string unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        var text = new StringBuilder(unit.Length * 100_000);
        for (int i = 0; i < 100_000; i++)
        {
            text.Append(unit);
        }

        CelPolicyParseResult result = CelPolicy.Parse(text.ToString(), "p.yaml");

        CelError error = Assert.Single(result.Errors);
        Assert.True(error.Code is CelErrorCode.InvalidYaml or CelErrorCode.InvalidPolicy, "The code is " + error.Code);
    }

    [Fact]
    public void DeepBlockNesting_OfThousandsOfLevelsIsAnErrorOfTheDepth()
    {
        var text = new StringBuilder();
        for (int i = 0; i < 12_000; i++)
        {
            text.Append("- ");
        }

        text.Append('a');

        YamlReadResult result = YamlReader.Read(text.ToString());

        Assert.True(result.IsError);
        Assert.Equal("exceeded max depth of 10000", result.ErrorMessage);
    }

    [Fact]
    public void ThousandNestedRules_AreReadWithoutRecursion()
    {
        const int Depth = 1000;
        var text = new StringBuilder("rule:\n");
        for (int level = 0; level < Depth; level++)
        {
            string margin = new(' ', 6 * level);
            text.Append(margin).Append("  match:\n");
            text.Append(margin).Append("    - rule:\n");
        }

        text.Append(' ', 6 * Depth).Append("  match:\n");
        text.Append(' ', 6 * Depth).Append("    - output: \"1\"\n");

        CelPolicy policy = CelPolicy.Parse(text.ToString(), "p.yaml").GetPolicyOrThrow();

        int depth = 0;
        for (PolicyRule? rule = policy.Document.Rule; rule is not null; rule = rule.Matches[0].Rule)
        {
            depth++;
        }

        Assert.Equal(Depth + 1, depth);
    }

    [Fact]
    public void DocumentOfAliasesThatStandsForAnEnormousPolicy_IsStopped()
    {
        var text = new StringBuilder();
        text.Append("description: &r0 {match: [{output: \"1\"}]}\n");
        text.Append("name: &r1 {match: [").Append(Repeat("{rule: *r0}, ", 10)).Append("{output: \"1\"}]}\n");
        for (int level = 2; level < 10; level++)
        {
            string previous = "*r" + (level - 1);
            text.Append("id").Append(level).Append(": &r").Append(level).Append(" {match: [").Append(Repeat("{rule: " + previous + "}, ", 10))
                .Append("{output: \"1\"}]}\n");
        }

        text.Append("rule: *r9\n");

        CelPolicyParseResult result = CelPolicy.Parse(text.ToString(), "p.yaml");

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Message.Contains("more than 100000", StringComparison.Ordinal));
    }

    // An alias on an expression stands for its whole text at every place it is used: the number of matches stays far below its limit
    // and the text that the compilation would parse is a thousand times the document, so the reader stops at the budget for text.
    [Fact]
    public void AliasOnAnExpressionOf60KilobytesUsed20000Times_IsStoppedByTheBudgetForText()
    {
        var text = new StringBuilder();
        text.Append("description: &e \"1").Append(Repeat(" + 1", 15_000)).Append("\"\nrule:\n  match:\n");
        for (int i = 0; i < 20_000; i++)
        {
            text.Append("    - output: *e\n");
        }

        var watch = Stopwatch.StartNew();
        CelPolicyParseResult result = CelPolicy.Parse(text.ToString(), "p.yaml");
        watch.Stop();

        AssertStoppedByTheBudget(result);
        Assert.True(watch.Elapsed < GuardTime, "The reading took " + watch.Elapsed);
    }

    // The explanation is not an expression, and a string in double quotes has a segment for each escape sequence: the characters and
    // the segments of every use are charged.
    [Fact]
    public void AliasOnAQuotedStringWithEscapesUsedAsAnExplanation_IsStoppedByTheBudgetForText()
    {
        var text = new StringBuilder();
        text.Append("description: &x \"").Append(Repeat("ab\\n", 15_000)).Append("\"\nrule:\n  match:\n");
        for (int i = 0; i < 20_000; i++)
        {
            text.Append("    - output: \"1\"\n      explanation: *x\n");
        }

        var watch = Stopwatch.StartNew();
        CelPolicyParseResult result = CelPolicy.Parse(text.ToString(), "p.yaml");
        watch.Stop();

        AssertStoppedByTheBudget(result);
        Assert.True(watch.Elapsed < GuardTime, "The reading took " + watch.Elapsed);
    }

    // A block scalar is cut from the document again at every use of its alias.
    [Fact]
    public void AliasOnABlockScalarUsedAsAnExplanation_IsStoppedByTheBudgetForText()
    {
        var text = new StringBuilder();
        text.Append("description: &x |\n");
        for (int i = 0; i < 600; i++)
        {
            text.Append("        ").Append(Repeat("abcdefghij", 10)).Append('\n');
        }

        text.Append("rule:\n  match:\n");
        for (int i = 0; i < 20_000; i++)
        {
            text.Append("    - output: \"1\"\n      explanation: *x\n");
        }

        var watch = Stopwatch.StartNew();
        CelPolicyParseResult result = CelPolicy.Parse(text.ToString(), "p.yaml");
        watch.Stop();

        AssertStoppedByTheBudget(result);
        Assert.True(watch.Elapsed < GuardTime, "The reading took " + watch.Elapsed);
    }

    // The message of an error that names a key repeats the key: an alias on a long scalar as the key of many unsupported entries
    // would give the memory of the number of entries times the length of the scalar.
    [Fact]
    public void AliasOnAKeyOf60KilobytesUsed20000Times_IsStoppedByTheBudgetForText()
    {
        var text = new StringBuilder();
        text.Append("description: &k \"").Append(Repeat("abcdefghij", 6_000)).Append("\"\nrule:\n");
        for (int i = 0; i < 20_000; i++)
        {
            text.Append("  *k : 1\n");
        }

        var watch = Stopwatch.StartNew();
        CelPolicyParseResult result = CelPolicy.Parse(text.ToString(), "p.yaml");
        watch.Stop();

        Assert.False(result.IsSuccess);
        Assert.Single(result.Errors, error => error.Message == TextLimitMessage);
        Assert.All(result.Errors, error => Assert.Equal(CelErrorCode.InvalidPolicy, error.Code));
        Assert.All(result.Errors, error => Assert.Equal(CelErrorStage.Parse, error.Stage));
        Assert.True(result.Errors.Length < 100, "The reader reported " + result.Errors.Length + " errors before it stopped.");
        Assert.True(watch.Elapsed < GuardTime, "The reading took " + watch.Elapsed);
    }

    [Fact]
    public void ExpressionsThatFillTheDocument_AreBelowTheBudgetForText()
    {
        string expression = "1" + Repeat(" + 1", 100_000);
        string document = "rule:\n  match:\n    - output: \"" + expression + "\"";

        CelPolicyParseResult result = CelPolicy.Parse(document, "p.yaml");

        Assert.True(result.IsSuccess);
    }

    // The strings with an escape sequence for every other character have a segment for each of them; the characters and the segments
    // together stay within the budget, which is four times the document.
    [Fact]
    public void StringsWithEscapesThatFillTheDocument_AreBelowTheBudgetForText()
    {
        string document = "rule:\n  match:\n    - condition: \"" + Repeat("a\\n", 20_000) + "\"\n      output: \"1\"\n      explanation: \""
            + Repeat("b\\t", 20_000) + "\"\n";

        CelPolicyParseResult result = CelPolicy.Parse(document, "p.yaml");

        Assert.True(result.IsSuccess);
    }

    // The 2 s of the requirement for a document of one megabyte are checked by CompileTimingTests, which takes the best of three runs.
    [Fact]
    public void DocumentOfOneMegabyte_IsReadInSeconds()
    {
        var text = new StringBuilder("rule:\n  match:\n");
        while (text.Length < 1_000_000)
        {
            text.Append("    - condition: \"x > 1 && y < 'abcdefgh'\"\n      output: \"'result of the match'\"\n");
        }

        var watch = Stopwatch.StartNew();
        CelPolicyParseResult result = CelPolicy.Parse(text.ToString(), "p.yaml");
        watch.Stop();

        Assert.True(result.IsSuccess, "The document of one megabyte is not a policy.");
        Assert.True(watch.Elapsed < GuardTime, "The reading took " + watch.Elapsed);
    }

    [Fact]
    public void ErrorsOfDocumentWithManyErrors_AreOrderedByPlace()
    {
        CelPolicyParseResult result = CelPolicy.Parse("foo: 1\nrule:\n  match:\n    - output: 1\nbar: 2", "p.yaml");

        Assert.Equal([1, 4, 5], Lines(result));
        Assert.Equal(3, result.Errors.Length);
    }

    // The one error of the budget for text, with nothing else reported.
    private static void AssertStoppedByTheBudget(CelPolicyParseResult result)
    {
        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.InvalidPolicy, error.Code);
        Assert.Equal(TextLimitMessage, error.Message);
        Assert.Equal(CelErrorStage.Parse, error.Stage);
    }

    private static int[] Lines(CelPolicyParseResult result)
    {
        var lines = new List<int>();
        foreach (CelError error in result.Errors)
        {
            lines.Add(Location(error).Line);
        }

        return [.. lines];
    }

    private static string Repeat(string unit, int count)
    {
        var text = new StringBuilder(unit.Length * count);
        for (int i = 0; i < count; i++)
        {
            text.Append(unit);
        }

        return text.ToString();
    }

    // Truncates the text, deletes, inserts pieces of YAML syntax and swaps lines, at random places.
    private static string Corrupt(TestRandom random, string text)
    {
        var builder = new StringBuilder(text);
        int changes = random.Next(1, 6);
        for (int i = 0; i < changes; i++)
        {
            switch (random.Next(4))
            {
                case 0:
                    builder.Length = random.Next(builder.Length + 1);
                    break;
                case 1 when builder.Length > 1:
                    int start = random.Next(builder.Length);
                    builder.Remove(start, random.Next(1, Math.Min(8, builder.Length - start) + 1));
                    break;
                case 2:
                    builder.Insert(random.Next(builder.Length + 1), Pieces[random.Next(Pieces.Length)]);
                    break;
                default:
                    builder = SwapLines(random, builder);
                    break;
            }
        }

        return builder.ToString();
    }

    private static StringBuilder SwapLines(TestRandom random, StringBuilder builder)
    {
        string[] lines = builder.ToString().Split('\n');
        int a = random.Next(lines.Length);
        int b = random.Next(lines.Length);
        (lines[a], lines[b]) = (lines[b], lines[a]);
        return new StringBuilder(string.Join('\n', lines));
    }
}

using System;
using System.Globalization;
using System.Text;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Policy.Tests.PolicyEnvironments;

namespace Great.Parts.Cel.Policy.Tests;

// A policy of an untrusted document compiles to a result and never to an exception, whatever its rules, and the depth of its rules or the
// number of its matches costs no stack: a policy of many nested rules is stopped by the limit, a policy of many matches is composed into
// slots, and a document of a megabyte compiles.
[Trait("Requirement", "R-13")]
public sealed class CompileUntrustedTests
{
    private const string Corpus = """
        name: corpus
        rule:
          variables:
            - name: a
              expression: >
                x +
                1
            - name: b
              expression: "variables.a * 2"
          match:
            - condition: "variables.b > 10"
              output: "'big'"
              explanation: 'it is big'
            - condition: variables.b > 5
              rule:
                id: nested
                match:
                  - output: !txt |
                      'medium'
            - output: "'small'"
        """;

    private static readonly string[] Pieces = [":", "-", "[", "]", "{", "}", ",", "&a", "*a", "!t", "|", ">", "'", "\"", "(", ")", "\n", " ", "\\", "x", "1", "variables.", "true"];

    [Fact]
    public void TheCorpus_CompilesAndEvaluates()
    {
        CelPolicyCompileResult<string> result = Compile<string>(Corpus);

        Assert.True(result.IsSuccess, string.Join('\n', result.Errors));
        Assert.Equal("big", Run(result, 10));
        Assert.Equal("medium", Run(result, 3));
        Assert.Equal("small", Run(result, 1));
    }

    [Fact]
    public void CorruptedPolicies_CompileToAResultWithoutAnException()
    {
        var random = new TestRandom(100);
        for (int i = 0; i < 1500; i++)
        {
            string text = Corrupt(random, Corpus);
            if (!CelPolicy.Parse(text, Source).IsSuccess)
            {
                continue;
            }

            for (int pass = 0; pass < 2; pass++)
            {
                CelPolicy policy = CelPolicy.Parse(text, Source).GetPolicyOrThrow();
                CelPolicyCompileResult<CelValue> result = (pass == 0 ? policy : policy.WithExplanationOutputs()).Compile<CelValue>(Env);

                Assert.Equal(result.IsSuccess, result.Errors.IsEmpty);
                Assert.All(result.Errors, error => Assert.True(error.SourceLocation is null || error.SourceLocation.Value.SourceName == Source));
            }
        }
    }

    [Fact]
    public void ThousandNestedRules_AreAnErrorOfTheLimitWithoutRecursion()
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

        CelPolicyCompileResult<CelValue> result = Compile<CelValue>(text.ToString());

        Assert.False(result.IsSuccess);
        CelError error = Assert.Single(result.Errors);
        Assert.Equal("rule exceeds nested expression limit", error.Message);
    }

    [Fact]
    public void TenThousandMatches_AreComposedIntoSlots()
    {
        const int Count = 10_000;
        var text = new StringBuilder("rule:\n  match:\n");
        for (int i = 0; i < Count; i++)
        {
            text.Append(CultureInfo.InvariantCulture, $"    - condition: \"x == {i}\"\n      output: \"'m{i}'\"\n");
        }

        text.Append("    - output: \"'none'\"\n");

        CelPolicyCompileResult<string> result = Compile<string>(text.ToString());

        Assert.True(result.IsSuccess, string.Join('\n', result.Errors));
        Assert.Equal("m0", RunOnLargeStack(result, 0));
        Assert.Equal("m9999", RunOnLargeStack(result, Count - 1));
        Assert.Equal("none", RunOnLargeStack(result, -1));
    }

    // The slots keep the tree of the policy shallow for the compiler, but the evaluation of a match at the end of ten thousand goes
    // through that many conditionals, one level of the stack each. A thread of the test runner has less stack on macOS than on Linux (the
    // run 633c7ad ended at about the 7700th level there with InsufficientStack, the guard of the library), so the evaluation has a stack
    // of its own, larger than any default.
    private static T RunOnLargeStack<T>(CelPolicyCompileResult<T> result, long x)
    {
        CelProgram<T> program = result.GetProgramOrThrow();
        CelEvalResult<T> evaluated = default;
        var thread = new Thread(
            () => evaluated = program.Evaluate(new CelBindings { ["x"] = x }, cancellationToken: CancellationToken.None),
            64 * 1024 * 1024);
        thread.Start();
        thread.Join();
        return evaluated.GetValueOrThrow();
    }

    // The depth of the nested rules that a document can have is bounded by the depth of its YAML, 10000 nodes: a nested rule written in
    // the flow style takes three, so 3000 levels are the deepest that the reader accepts at a round number.
    [Fact]
    public void ThreeThousandNestedRulesInTheFlowStyle_AreAnErrorOfTheLimitWithoutRecursion()
    {
        const int Depth = 3000;
        var text = new StringBuilder("rule: ");
        for (int level = 0; level < Depth; level++)
        {
            text.Append("{match: [{rule: ");
        }

        text.Append("{match: [{output: \"1\"}]}");
        for (int level = 0; level < Depth; level++)
        {
            text.Append("}]}");
        }

        CelPolicyCompileResult<CelValue> result = Compile<CelValue>(text.ToString());

        Assert.False(result.IsSuccess);
        CelError error = Assert.Single(result.Errors);
        Assert.Equal("rule exceeds nested expression limit", error.Message);
    }

    [Fact]
    public void ManyVariables_AreStoppedByTheLimit()
    {
        const int Count = 5_000;
        var text = new StringBuilder("rule:\n  variables:\n");
        for (int i = 0; i < Count; i++)
        {
            text.Append(CultureInfo.InvariantCulture, $"    - name: v{i}\n      expression: \"variables.v{Math.Max(0, i - 1)} + 1\"\n");
        }

        text.Append("  match:\n    - output: \"1\"\n");

        CelPolicyCompileResult<CelValue> result = Compile<CelValue>(text.ToString());

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, error => error.Message == "variable exceeds nested expression limit");
    }

    // Truncates the text, deletes and inserts pieces, at random places.
    private static string Corrupt(TestRandom random, string text)
    {
        var builder = new StringBuilder(text);
        int changes = random.Next(1, 4);
        for (int i = 0; i < changes; i++)
        {
            switch (random.Next(3))
            {
                case 0:
                    builder.Length = random.Next(builder.Length + 1);
                    break;
                case 1 when builder.Length > 1:
                    int start = random.Next(builder.Length);
                    builder.Remove(start, random.Next(1, Math.Min(8, builder.Length - start) + 1));
                    break;
                default:
                    builder.Insert(random.Next(builder.Length + 1), Pieces[random.Next(Pieces.Length)]);
                    break;
            }
        }

        return builder.ToString();
    }
}

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Policy.Tests.PolicyEnvironments;

namespace Great.Parts.Cel.Policy.Tests;

// The collection of the tests that measure a time: it runs alone, so that the other tests of the assembly do not take the processors.
[CollectionDefinition(Name, DisableParallelization = true)]
[SuppressMessage("Design", "CA1515:Consider making public types internal", Justification = "xUnit requires a collection definition class to be public.")]
public sealed class TimingGroup
{
    public const string Name = "Timing";
}

// A policy of a document of one megabyte is read and compiled together in four seconds, in the semantic of the first match and in the
// aggregate one: the compilation is linear in the size of the document. The limit is that of the requirement, and the time compared
// with it is the best of three runs after a warm-up: a run that a neighbour on a shared machine slows down does not fail the test, a
// compilation that is not linear does, in every run.
[Collection(TimingGroup.Name)]
[Trait("Requirement", "R-13")]
public sealed class CompileTimingTests
{
    [Theory]
    [InlineData("match")]
    [InlineData("aggregate")]
    public static void DocumentOfOneMegabyte_IsReadAndCompiledInFourSeconds(string semantic)
    {
        // Smaller documents first, so that the time is not that of the first calls of the code.
        Assert.True(Compile<CelValue>(MatchesDocument(semantic, 200_000)).IsSuccess);
        Assert.True(Compile<CelValue>(MatchesDocument(semantic, 200_000)).IsSuccess);

        string document = MatchesDocument(semantic, 1_000_000);
        TimeSpan[] times = new TimeSpan[3];
        TimeSpan best = TimeSpan.MaxValue;
        for (int run = 0; run < times.Length; run++)
        {
            var watch = Stopwatch.StartNew();
            CelPolicyCompileResult<CelValue> result = Compile<CelValue>(document);
            watch.Stop();

            Assert.True(result.IsSuccess, result.Errors.IsEmpty ? string.Empty : result.Errors[0].Message);
            times[run] = watch.Elapsed;
            best = watch.Elapsed < best ? watch.Elapsed : best;
        }

        Assert.True(
            best < TimeSpan.FromSeconds(4),
            "The reading and the compilation took " + times[0] + ", " + times[1] + " and " + times[2] + "; the best of them is the one compared.");
    }

    private static string MatchesDocument(string semantic, int length)
    {
        var text = new StringBuilder("rule:\n  ").Append(semantic).Append(":\n");
        int count = 0;
        while (text.Length < length)
        {
            text.Append(CultureInfo.InvariantCulture, $"    - condition: \"x == {count}\"\n      output: \"'m{count}'\"\n");
            count++;
        }

        return text.ToString();
    }
}

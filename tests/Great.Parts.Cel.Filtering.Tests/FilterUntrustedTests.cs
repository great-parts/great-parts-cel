using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Xunit;
using static Great.Parts.Cel.Filtering.Tests.LibraryEnvironments;

namespace Great.Parts.Cel.Filtering.Tests;

// The compiler on untrusted filters: random texts and filters of the grammar, up to the limit of the length, give a program or errors,
// never an exception or an internal error, each in less than a second; a field or a function argument of 32 000 parts is
// NestingTooDeep in less than a second; 5 000 restrictions joined by AND compile to a program.
[Trait("Requirement", "R-15")]
[Trait("Requirement", "F-32")]
public sealed class FilterUntrustedTests
{
    private const int Cases = 1_000;
    private const int Limit = 65_536;

    [Fact]
    public void RandomText_IsAProgramOrErrors_NeverAnException()
    {
        var random = new FilterGenerator.Source(20261129);
        for (int i = 0; i < Cases; i++)
        {
            string text = FilterGenerator.RandomText(random, random.Next(0, 4) == 0 ? random.Next(0, Limit + 1) : random.Next(0, 80));

            CheckResult(text);
        }
    }

    [Fact]
    public void FilterOfTheGrammar_IsAProgramOrErrors_NeverAnException()
    {
        var random = new FilterGenerator.Source(20261130);
        for (int i = 0; i < Cases; i++)
        {
            StringBuilder filter = new();
            FilterGenerator.Expression(random, filter, 4);

            CheckResult(filter.ToString());
        }
    }

    [Fact]
    public void FilterOfTheGrammarOfTheLimitOfTheLength_IsAProgramOrErrorsInLessThanASecond()
    {
        var random = new FilterGenerator.Source(20261201);
        StringBuilder filter = new();
        while (filter.Length < Limit - 200)
        {
            if (filter.Length > 0)
            {
                filter.Append(" AND ");
            }

            FilterGenerator.Restriction(random, filter);
        }

        // The least of three measurements of the same filter: one hiccup of the scheduler on a slow runner is not the time of the compiler.
        string text = filter.ToString();
        CheckResult("pages = 1");
        TimeSpan least = TimeSpan.MaxValue;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            Stopwatch watch = Stopwatch.StartNew();
            CheckResult(text);
            watch.Stop();
            least = watch.Elapsed < least ? watch.Elapsed : least;
        }

        Assert.True(least < TimeSpan.FromSeconds(1), "took " + least);
    }

    [Fact]
    public void NestedParentheses_AreAnErrorWithoutAStackOverflow()
    {
        // The length is checked first: 100 000 parentheses are longer than the limit and are not read.
        CelError tooLong = Assert.Single(F.Compile(new string('(', 100_000)).Errors);

        Assert.Equal(CelErrorCode.ExpressionTooLong, tooLong.Code);

        // 65 000 parentheses are within the limit of the length: the 251st opening one is the error.
        CelError tooDeep = Assert.Single(F.Compile(new string('(', 65_000)).Errors);

        Assert.Equal(CelErrorCode.NestingTooDeep, tooDeep.Code);
        Assert.Equal(250, tooDeep.Span.Start.Offset);
        Assert.Equal(1, tooDeep.Span.Length);
    }

    [Fact]
    public void FieldOf32000Parts_IsNestingTooDeepInLessThanASecond()
    {
        string path = LongPath(32_000);

        (CelCompileResult<bool> result, TimeSpan least) = CompileTimed(path + " = 1");

        Assert.Equal((CelErrorCode.NestingTooDeep, 0, path.Length), FirstError(result.Errors));
        Assert.True(least < TimeSpan.FromSeconds(1), "took " + least);
    }

    [Fact]
    public void FunctionArgumentOf32000Parts_IsNestingTooDeepInLessThanASecond()
    {
        string path = LongPath(32_000);

        (CelCompileResult<bool> result, TimeSpan least) = CompileTimed("lower(" + path + ") = \"x\"");

        Assert.Equal((CelErrorCode.NestingTooDeep, 6, path.Length), FirstError(result.Errors));
        Assert.True(least < TimeSpan.FromSeconds(1), "took " + least);
    }

    [Fact]
    public void FiveThousandRestrictionsJoinedByAnd_AreAProgram()
    {
        string filter = string.Join(" AND ", Enumerable.Repeat("pages=1", 5_000));

        CelCompileResult<bool> result = F.Compile(filter);

        Assert.True(filter.Length <= Limit);
        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Take(3)));
    }

    // A path that starts with the variable title and goes on through the given number of parts in all.
    private static string LongPath(int parts) => "title" + string.Concat(Enumerable.Repeat(".a", parts - 1));

    // The result of a filter and the least of three measurements of its compilation.
    private static (CelCompileResult<bool> Result, TimeSpan Least) CompileTimed(string filter)
    {
        CelCompileResult<bool> result = F.Compile(filter);
        TimeSpan least = TimeSpan.MaxValue;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            Stopwatch watch = Stopwatch.StartNew();
            result = F.Compile(filter);
            watch.Stop();
            least = watch.Elapsed < least ? watch.Elapsed : least;
        }

        return (result, least);
    }

    // The compilation of a filter: a program, which evaluates without an exception, or errors that are not internal.
    private static void CheckResult(string filter)
    {
        CelCompileResult<bool> result = F.Compile(filter);

        if (result.IsSuccess)
        {
            _ = result.GetProgramOrThrow().EvaluateInput(Dune, cancellationToken: CancellationToken.None);
            return;
        }

        Assert.False(result.Errors.IsEmpty);
        Assert.DoesNotContain(result.Errors, error => error.Code == CelErrorCode.InternalError);
    }
}

using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Great.Parts.Cel.Diagnostics;
using Xunit;

namespace Great.Parts.Cel.Properties.Evaluation;

// The asynchronous evaluation equals the synchronous one with the synchronous twins of the functions (docs/design/tests.md, "Properties of
// evaluation"): the same value, the same class of outcome, the same code and place of the error when the expression has one node that
// may fail, a cost that is the same in every repeat, every call run once per its node and arguments, and a program that needs no waiting
// completes in its call. An expression with several nodes that may fail has an error to the right of a call that waits, which takes
// precedence over the error that call would give: there only the value and the class of the outcome are the same.
public sealed class AsyncEqualsSyncTests
{
    private const ulong Seed = 20261123;
    private const int Cases = 250;
    private const ulong Limit = 1_000_000_000;

    [Fact]
    [Trait("Requirement", "R-9")]
    [Trait("Requirement", "F-23")]
    public void SameValues()
    {
        var calls = new AsyncCalls();
        PropertyLoop.Check(nameof(SameValues), AsyncExpressions.Of(AsyncMode.Values), Seed, Cases, testCase => Violation(calls, testCase, sameSource: true), testCase => testCase.ToString());
    }

    [Fact]
    [Trait("Requirement", "R-9")]
    [Trait("Requirement", "F-23")]
    public void SameErrorsOfFunctions()
    {
        var calls = new AsyncCalls();
        PropertyLoop.Check(nameof(SameErrorsOfFunctions), AsyncExpressions.Of(AsyncMode.FailingFunctions), Seed + 1, Cases, testCase => Violation(calls, testCase, sameSource: true), testCase => testCase.ToString());
    }

    [Fact]
    [Trait("Requirement", "R-9")]
    [Trait("Requirement", "F-23")]
    public void SameErrorsOfOperations()
    {
        var calls = new AsyncCalls();
        PropertyLoop.Check(nameof(SameErrorsOfOperations), AsyncExpressions.Of(AsyncMode.FailingOperations), Seed + 2, Cases, testCase => Violation(calls, testCase, sameSource: true), testCase => testCase.ToString());
    }

    [Fact]
    [Trait("Requirement", "R-9")]
    [Trait("Requirement", "F-23")]
    public void SameClassOfOutcomeWithBothSourcesOfErrors()
    {
        var calls = new AsyncCalls();
        PropertyLoop.Check(nameof(SameClassOfOutcomeWithBothSourcesOfErrors), AsyncExpressions.Of(AsyncMode.Mixed), Seed + 3, Cases, testCase => Violation(calls, testCase, sameSource: false), testCase => testCase.ToString());
    }

    [Fact]
    [Trait("Requirement", "F-23")]
    [Trait("Requirement", "R-9")]
    public void ProgramWithoutCalls_CompletesInItsCall()
    {
        var calls = new AsyncCalls();
        PropertyLoop.Check(nameof(ProgramWithoutCalls_CompletesInItsCall), AsyncExpressions.Of(AsyncMode.WithoutCalls), Seed + 4, Cases, testCase => Violation(calls, testCase, sameSource: true), testCase => testCase.ToString());
    }

    private static string? Violation(AsyncCalls calls, AsyncCase testCase, bool sameSource)
    {
        CelProgram<long> sync = calls.Sync.Compile<long>(testCase.Text).GetProgramOrThrow();
        CelProgram<long> asynchronous = calls.Async.Compile<long>(testCase.Text).GetProgramOrThrow();
        var options = new CelEvalOptions { CostLimit = Limit };
        CelEvalResult<long> expected = sync.Evaluate(AsyncCalls.Bindings(), options, CancellationToken.None);

        calls.Reset();
        ValueTask<CelEvalResult<long>> task = asynchronous.EvaluateAsync(AsyncCalls.Bindings(), options, CancellationToken.None);
        if (!testCase.HasCalls && !task.IsCompletedSuccessfully)
        {
            return "a program without calls did not complete in its call";
        }

        CelEvalResult<long> actual = task.AsTask().GetAwaiter().GetResult();
        if (Overrun(calls, testCase.Text) is { } overrun)
        {
            return overrun;
        }

        if (Different(expected, actual, sameSource) is { } difference)
        {
            return difference;
        }

        // The same evaluation again, with other delays: the same outcome and the same cost.
        CelEvalResult<long> again = asynchronous.EvaluateAsync(AsyncCalls.Bindings(), options, CancellationToken.None).AsTask().GetAwaiter().GetResult();
        return Different(actual, again, sameSource: true) ?? (actual.Cost == again.Cost ? null : $"cost {actual.Cost} and then {again.Cost}");
    }

    // A call runs once for its node and its arguments: with the function written n times in the text, a call with one argument cannot
    // run more than n times.
    private static string? Overrun(AsyncCalls calls, string text)
    {
        foreach (((string function, long argument), int count) in calls.Calls)
        {
            int nodes = Occurrences(text, function + "(");
            if (count > nodes)
            {
                return string.Create(CultureInfo.InvariantCulture, $"{function}({argument}) ran {count} times, the text has {nodes} calls of {function}");
            }
        }

        return null;
    }

    private static int Occurrences(string text, string part)
    {
        int count = 0;
        for (int at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + part.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    private static string? Different(CelEvalResult<long> expected, CelEvalResult<long> actual, bool sameSource)
    {
        if (expected.IsSuccess != actual.IsSuccess)
        {
            return $"{Describe(expected)} instead of {Describe(actual)}";
        }

        if (expected.IsSuccess)
        {
            return expected.Value == actual.Value ? null : $"{Describe(expected)} instead of {Describe(actual)}";
        }

        CelError want = expected.Errors.Single();
        CelError got = actual.Errors.Single();
        return !sameSource || (want.Code, want.Location.Offset) == (got.Code, got.Location.Offset)
            ? null
            : $"{Describe(expected)} instead of {Describe(actual)}";
    }

    private static string Describe(CelEvalResult<long> result) =>
        result.IsSuccess
            ? result.Value.ToString(CultureInfo.InvariantCulture)
            : $"{result.Errors.Single().Code} at {result.Errors.Single().Location.Offset.ToString(CultureInfo.InvariantCulture)}";
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Examples.Library;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Facade;

// matches through the facade, and the cache of patterns that nobody can see. The cache of patterns is part
// of an environment, shared by its programs; ToBuilder() and a new environment start with an empty one, and
// the compilation cache of the application keeps the failed compilation of a constant pattern like any other.
// What the cache must not change, whatever it holds or has evicted: the value or the error of an evaluation
// and its cost, as the cost depends on the text of the expression and the data (a pattern found in the cache
// is charged for its parse and its build as if it were not, and so is a refused one). The examples compare
// the first evaluation, which has no pattern in the cache, with later ones, one environment with another
// and with the environment of ToBuilder(); the cost of an evaluation is the least limit that does not give
// CostLimitExceeded. The capacities of the cache are internal constants calibrated at stage 0 (128 patterns
// and 100 000 instructions in total weight), so the eviction example does not rely on them: it runs far more
// patterns and instructions than any capacity that is within reason holds, and the result must be the same
// whichever patterns are in the cache at the moment.
public sealed class MatchesFacadeTests
{
    private static readonly (string Expression, string Subject, string Pattern)[] Scenarios =
    [
        ("matches(s, 'abc')", "xabcx", "abc"),
        ("matches(s, p)", "xabcx", "abc"),
        ("s.matches(p)", "xabcx", "^x"),
        ("matches(s, p)", "x\U0001F600", "^x.$"),
        ("matches(s, p)", "abc", "("),
        ("s.matches(p)", "abc", "a**"),
        ("matches(s, '(')", "abc", "-"),
        ("matches(s, p)", "x", "a{1000}a{1000}a{1000}a{1000}a{1000}"),
        ("matches(s, p)", "x", "[a-z]{1000}[a-z]{1000}[a-z]{1000}[a-z]{1000}[a-z]{1000}[a-z]{1000}[a-z]{1000}[a-z]{1000}[a-z]{1000}[a-z]{1000}"),
    ];

    private readonly CelEnvironment environment = Build();

    [Fact]
    [Trait("Requirement", "F-6")]
    [Trait("Requirement", "F-4")]
    public void OneStep_GivesTheCompileErrorOfAConstantPatternAndTheEvaluationErrorOfAVariable()
    {
        CelEvalResult<bool> constant = environment.Evaluate<bool>("matches('abc', '(')", new CelBindings(), cancellationToken: CancellationToken.None);
        CelError compileError = Assert.Single(constant.Errors);
        Assert.Equal((CelErrorCode.InvalidRegex, CelErrorStage.Check, 15), (compileError.Code, compileError.Stage, compileError.Location.Offset));

        CelEvalResult<bool> variable = environment.Evaluate<bool>("matches(s, p)", new CelBindings().Add("s", "abc").Add("p", "("), cancellationToken: CancellationToken.None);
        CelError evaluationError = Assert.Single(variable.Errors);
        Assert.Equal((CelErrorCode.EvalInvalidRegex, CelErrorStage.Evaluate, 7), (evaluationError.Code, evaluationError.Stage, evaluationError.Location.Offset));

        CelEvalResult<bool> value = environment.Evaluate<bool>("s.matches(p)", new CelBindings().Add("s", "xabcx").Add("p", "b+c"), cancellationToken: CancellationToken.None);
        Assert.True(value.GetValueOrThrow());
    }

    [Fact]
    [Trait("Requirement", "P-2")]
    [Trait("Requirement", "R-4")]
    public void CompilationCache_KeepsTheFailedCompilationOfAConstantPattern()
    {
        var cache = new CelCompilationCache(environment, 10, 1000);

        CelCompileResult<bool> first = cache.GetOrCompile<bool>("matches('abc', '(')");

        Assert.False(first.IsSuccess);
        Assert.Equal(CelErrorCode.InvalidRegex, Assert.Single(first.Errors).Code);
        Assert.Same(first, cache.GetOrCompile<bool>("matches('abc', '(')"));
        Assert.True(cache.Evaluate<bool>("matches(s, p)", new CelBindings().Add("s", "abc").Add("p", "b"), cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    [Fact]
    [Trait("Requirement", "R-4")]
    [Trait("Requirement", "R-3")]
    public void SameExpression_InAnotherEnvironmentAndInToBuilder_GivesTheSameOutcomeAndCost()
    {
        foreach ((string expression, string subject, string pattern) in Scenarios)
        {
            CelBindings bindings = new CelBindings().Add("s", subject).Add("p", pattern);
            string first = MatchesRows.Outcome(environment, expression, bindings);
            string second = MatchesRows.Outcome(environment, expression, bindings);

            Assert.Equal(first, second);
            Assert.Equal(first, MatchesRows.Outcome(Build(), expression, bindings));
            Assert.Equal(first, MatchesRows.Outcome(environment.ToBuilder().Build().GetEnvironmentOrThrow(), expression, bindings));
            if (first.StartsWith("compile", StringComparison.Ordinal))
            {
                continue;
            }

            ulong cost = CostOf(environment, expression, bindings);
            foreach (CelEnvironment fresh in new[] { Build(), environment.ToBuilder().Build().GetEnvironmentOrThrow() })
            {
                // The first evaluation in a new environment finds no pattern in its cache, the next ones find it.
                Assert.True(Reaches(fresh, expression, bindings, cost));
                Assert.True(Reaches(fresh, expression, bindings, cost));
                Assert.False(Reaches(fresh, expression, bindings, cost - 1));
            }
        }
    }

    [Fact]
    [Trait("Requirement", "R-4")]
    [Trait("Requirement", "P-2")]
    public void PatternCache_DoesNotChangeResults_WhateverIsEvicted()
    {
        CelProgram<CelValue> program = environment.Compile<CelValue>("matches(s, p)").GetProgramOrThrow();
        string[] large = Enumerable.Range(0, 6).Select(index => string.Concat(Enumerable.Repeat("a{1000}", 50)) + new string('b', index + 1)).ToArray();
        var failures = new List<string>();
        for (int round = 0; round < 2; round++)
        {
            for (int index = 0; index < 3000; index++)
            {
                string number = index.ToString(CultureInfo.InvariantCulture);
                string pattern = "^v" + number + "$";
                AddFailure(failures, program, "v" + number, pattern, true);
                AddFailure(failures, program, "v" + (index + 1).ToString(CultureInfo.InvariantCulture), pattern, false);
                if (index % 500 == 0)
                {
                    AddFailure(failures, program, "x", large[index / 500], false);
                }
            }
        }

        Assert.Empty(failures);
    }

    private static CelEnvironment Build() => new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<string>("s")
        .AddVariable<string>("p")
        .Build()
        .GetEnvironmentOrThrow();

    private static void AddFailure(List<string> failures, CelProgram<CelValue> program, string subject, string pattern, bool expected)
    {
        CelEvalResult<CelValue> result = program.Evaluate(new CelBindings().Add("s", subject).Add("p", pattern), cancellationToken: CancellationToken.None);
        if (!result.IsSuccess || !result.GetValueOrThrow().TryGetBoolean(out bool actual) || actual != expected)
        {
            failures.Add(subject + " | " + pattern[..Math.Min(pattern.Length, 20)]);
        }
    }

    // Whether the evaluation with the cost limit gets to its end: the limit is not exceeded.
    private static bool Reaches(CelEnvironment environment, string expression, CelBindings bindings, ulong limit)
    {
        CelProgram<CelValue> program = environment.Compile<CelValue>(expression).GetProgramOrThrow();
        CelEvalResult<CelValue> result = program.Evaluate(bindings, new CelEvalOptions { CostLimit = limit }, CancellationToken.None);
        return !result.Errors.Any(error => error.Code == CelErrorCode.CostLimitExceeded);
    }

    // The least limit that is not exceeded: the cost of the evaluation, found by bisection.
    private static ulong CostOf(CelEnvironment environment, string expression, CelBindings bindings)
    {
        ulong low = 0;
        ulong high = 1_000_000;
        while (low < high)
        {
            ulong middle = low + ((high - low) / 2);
            if (Reaches(environment, expression, bindings, middle))
            {
                high = middle;
            }
            else
            {
                low = middle + 1;
            }
        }

        return low;
    }
}

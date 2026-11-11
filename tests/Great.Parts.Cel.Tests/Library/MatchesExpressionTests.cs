using System.Linq;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Library;

// matches through compiled expressions: the place and the text of its two errors, what a whole expression costs and that a cache does
// not change it, the cache of an environment and of the environment that ToBuilder gives, and an overload of the application that
// takes the call away from the preparation of a literal.
//
// Variables: s: string, p: string. The offsets of the errors are those of the call or of the literal in the text of the expression.
public sealed class MatchesExpressionTests
{
    // The expression, the offset of the literal of the pattern, the offset in the pattern and the reason. The offset in the pattern
    // counts UTF-16 code units of the value of the literal after the escapes of CEL are read, not of its text.
    [Theory]
    [InlineData("matches('abc', '(')", 15, 0, "missing closing )")]
    [InlineData("'abc'.matches('(')", 14, 0, "missing closing )")]
    [InlineData("matches('abc', r'(')", 15, 0, "missing closing )")]
    [InlineData("matches('abc', '''(''')", 15, 0, "missing closing )")]
    [InlineData("matches('abc', \"(\")", 15, 0, "missing closing )")]
    [InlineData("matches('abc', ('('))", 16, 0, "missing closing )")]
    [InlineData("matches('abc', '\\x28')", 15, 0, "missing closing )")]
    [InlineData("matches('abc', '\\U0001F600a**')", 15, 3, "invalid nested repetition operator")]
    [InlineData("matches('abc', '\\u00e9\\u00e9a**')", 15, 3, "invalid nested repetition operator")]
    [InlineData("'abc'.matches('a{1001}')", 14, 1, "invalid repeat count")]
    [InlineData("false && matches('abc', '(')", 24, 0, "missing closing )")]
    [InlineData("['a'].all(x, matches(x, '('))", 24, 0, "missing closing )")]
    [InlineData("true ? true : matches('abc', '(')", 29, 0, "missing closing )")]
    public static void ConstantPattern_WithAnError_IsInvalidRegexAtTheLiteral(string expression, int place, int offset, string reason)
    {
        CelCompileResult<bool> compiled = NewEnvironment().Compile<bool>(expression);

        CelError error = Assert.Single(compiled.Errors);
        Assert.Equal(
            (CelErrorCode.InvalidRegex, CelErrorStage.Check, place, "invalid regular expression at offset " + offset + ": " + reason),
            (error.Code, error.Stage, error.Location.Offset, error.Message));
    }

    // The check goes on after the refusal of a literal: every error of the text is returned, in the order of the text, and a pattern
    // that is not a literal is no error.
    [Fact]
    public static void ConstantPattern_WithAnError_DoesNotStopTheCheck()
    {
        CelCompileResult<bool> compiled = NewEnvironment().Compile<bool>("matches('abc', '(') && matches(1, 'b') && matches('a', 'a**') && matches(s, p)");

        (CelErrorCode Code, int Offset)[] errors = [.. compiled.Errors.Select(error => (error.Code, error.Location.Offset))];

        Assert.Equal(
            new[] { (CelErrorCode.InvalidRegex, 15), (CelErrorCode.NoMatchingOverload, 30), (CelErrorCode.InvalidRegex, 55) },
            errors);
    }

    // A pattern that is not a string literal is not looked at when the expression is checked, even when its value is known.
    [Theory]
    [InlineData("matches('abc', dyn('('))")]
    [InlineData("matches('abc', string('('))")]
    [InlineData("matches('abc', '(' + '')")]
    [InlineData("matches('abc', true ? '(' : 'a')")]
    [InlineData("matches('abc', ['('][0])")]
    [InlineData("matches('abc', p)")]
    public static void PatternThatIsNotALiteral_IsNoCheckError(string expression)
    {
        Assert.True(NewEnvironment().Compile<bool>(expression).IsSuccess);
    }

    // The error of a pattern from data is at the call, the "(" after the name for the receiver form and the macro too, with the text
    // of the error of a literal.
    [Theory]
    [InlineData("matches(s, p)", "(", 7, 0, "missing closing )")]
    [InlineData("s.matches(p)", "(", 9, 0, "missing closing )")]
    [InlineData("matches(s, p)", "a**", 7, 1, "invalid nested repetition operator")]
    [InlineData("s.matches(p)", "a{1001}", 9, 1, "invalid repeat count")]
    [InlineData("matches('abc', dyn('('))", "", 7, 0, "missing closing )")]
    [InlineData("matches('abc', '(' + '')", "", 7, 0, "missing closing )")]
    [InlineData("'abc'.matches('a' + '(')", "", 13, 0, "missing closing )")]
    [InlineData("['a', 'b'].all(x, matches(x, p))", "(", 25, 0, "missing closing )")]
    public static void PatternFromData_WithAnError_IsEvalInvalidRegexAtTheCall(string expression, string pattern, int place, int offset, string reason)
    {
        CelEvalResult<bool> result = Evaluate(NewEnvironment(), expression, "x", pattern);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(
            (CelErrorCode.EvalInvalidRegex, CelErrorStage.Evaluate, place, "invalid regular expression at offset " + offset + ": " + reason),
            (error.Code, error.Stage, error.Location.Offset, error.Message));
    }

    // The units of whole expressions, s = 'xabcx' (n = 5), p = 'abc' (L = 3, m = 4): the reads of the variables 1 each, a call of dyn
    // 1, a literal 0, and the call of matches by its charges — as a literal the match 4, otherwise the parse 2, the build 1 and the
    // match 4. A dyn operand is no change of the charges. A second evaluation of the same program, which finds the pattern in the
    // cache, charges the same.
    [Theory]
    [InlineData("matches(s, 'abc')", 5UL)]
    [InlineData("s.matches('abc')", 5UL)]
    [InlineData("matches(s, p)", 9UL)]
    [InlineData("s.matches(p)", 9UL)]
    [InlineData("matches(dyn(s), dyn(p))", 11UL)]
    [InlineData("dyn(s).matches(p)", 10UL)]
    [InlineData("dyn(s).matches('abc')", 6UL)]
    [InlineData("matches(s, dyn('abc'))", 9UL)]
    [InlineData("matches(s, 'a' + 'bc')", 11UL)]
    [InlineData("matches('xabcx', 'abc')", 4UL)]
    public static void Call_CostsItsStepsAndTheCacheChangesNothing(string expression, ulong units)
    {
        CelEnvironment environment = NewEnvironment();
        Plan plan = environment.Compile<bool>(expression).GetProgramOrThrow().Plan;
        CelBindings bindings = new CelBindings().Add("s", "xabcx").Add("p", "abc");

        for (int evaluation = 1; evaluation <= 2; evaluation++)
        {
            Frame frame = plan.CreateFrame(bindings.Values, ulong.MaxValue, CancellationToken.None);
            CelValue value = plan.Run(frame);

            Assert.True(value.TryGetBoolean(out bool result) && result, expression);
            Assert.Equal(units, frame.ChargedUnits);
        }

        // The total is exact: the limit that equals it runs the call and one unit less stops it.
        CelProgram<bool> program = environment.Compile<bool>(expression).GetProgramOrThrow();
        Assert.True(program.Evaluate(bindings, new CelEvalOptions { CostLimit = units }, CancellationToken.None).IsSuccess);
        Assert.Equal(CelErrorCode.CostLimitExceeded, Assert.Single(program.Evaluate(bindings, new CelEvalOptions { CostLimit = units - 1 }, CancellationToken.None).Errors).Code);
    }

    // The refused pattern from data costs the parse alone, whether or not the refusal is in the cache.
    [Fact]
    public static void RefusedPatternFromData_CostsTheParseAlone_EachTime()
    {
        Plan plan = NewEnvironment().Compile<bool>("matches(s, p)").GetProgramOrThrow().Plan;
        CelBindings bindings = new CelBindings().Add("s", "x").Add("p", "(");

        for (int evaluation = 1; evaluation <= 2; evaluation++)
        {
            Frame frame = plan.CreateFrame(bindings.Values, ulong.MaxValue, CancellationToken.None);
            CelValue value = plan.Run(frame);

            Assert.True(value.TryGetError(out CelError? error));
            Assert.Equal((CelErrorCode.EvalInvalidRegex, 4UL), (error.Code, frame.ChargedUnits));
        }
    }

    // matches on operands of the static type dyn: the overload is chosen by the values, in both forms, and a value that is not a
    // string is EvalNoMatchingOverload that names the types of the values.
    [Theory]
    [InlineData("matches(dyn(1), 'a')", "no such overload: matches(int, string)")]
    [InlineData("matches('a', dyn(1))", "no such overload: matches(string, int)")]
    [InlineData("matches(dyn(b'a'), dyn(['a']))", "no such overload: matches(bytes, list)")]
    public static void DynOperand_OfAnotherKind_IsEvalNoMatchingOverload(string expression, string message)
    {
        CelEvalResult<bool> result = Evaluate(NewEnvironment(), expression, "x", "a");

        CelError error = Assert.Single(result.Errors);
        Assert.Equal((CelErrorCode.EvalNoMatchingOverload, message), (error.Code, error.Message));
    }

    // A new environment has an empty cache of its own. A literal pattern is kept by the check, the same pattern from data finds that
    // entry, and two programs of one environment share the cache.
    [Fact]
    public static void Cache_IsOfTheEnvironmentAndSharedByItsPrograms()
    {
        CelEnvironment environment = NewEnvironment();
        Assert.Equal(0, environment.PatternCache.Count);

        _ = environment.Compile<bool>("matches(s, 'abc')").GetProgramOrThrow();
        Assert.Equal(1, environment.PatternCache.Count);

        _ = environment.Compile<bool>("s.matches('abc')").GetProgramOrThrow();
        Assert.Equal(1, environment.PatternCache.Count);

        CelProgram<bool> fromData = environment.Compile<bool>("matches(s, p)").GetProgramOrThrow();
        Assert.True(fromData.Evaluate(new CelBindings().Add("s", "xabcx").Add("p", "abc"), null, CancellationToken.None).GetValueOrThrow());
        Assert.Equal(1, environment.PatternCache.Count);

        Assert.True(fromData.Evaluate(new CelBindings().Add("s", "xbx").Add("p", "b+"), null, CancellationToken.None).GetValueOrThrow());
        Assert.Equal(2, environment.PatternCache.Count);
    }

    // A refused literal is kept too: its second compilation finds the refusal.
    [Fact]
    public static void Cache_KeepsTheRefusalOfALiteral()
    {
        CelEnvironment environment = NewEnvironment();

        _ = environment.Compile<bool>("matches('abc', '(')");
        _ = environment.Compile<bool>("matches('abc', '(')");

        Assert.Equal((1, 1), (environment.PatternCache.Count, environment.PatternCache.Weight));
    }

    // ToBuilder and a new builder each give an environment with an empty cache of its own: what one keeps the other does not see.
    [Fact]
    public static void Cache_OfToBuilder_IsEmptyAndIndependent()
    {
        CelEnvironment first = NewEnvironment();
        _ = first.Compile<bool>("matches(s, 'abc')").GetProgramOrThrow();

        CelEnvironment second = first.ToBuilder().Build().GetEnvironmentOrThrow();
        CelEnvironment third = NewEnvironment();

        Assert.NotSame(first.PatternCache, second.PatternCache);
        Assert.NotSame(second.PatternCache, third.PatternCache);
        Assert.Equal((1, 0, 0), (first.PatternCache.Count, second.PatternCache.Count, third.PatternCache.Count));

        _ = second.Compile<bool>("matches(s, 'b+')").GetProgramOrThrow();
        Assert.Equal((1, 1, 0), (first.PatternCache.Count, second.PatternCache.Count, third.PatternCache.Count));
        Assert.Same(first.PatternCache, first.Declarations.PatternCache);
        Assert.Same(second.PatternCache, second.Declarations.PatternCache);
    }

    // An overload of the application named matches whose parameters are not string, string does not take a call that only the library
    // overload can take: its literal is prepared and refused by the check. A call that both overloads can take — through a dyn
    // subject — is left to evaluation, which chooses by the values: the literal is not prepared, the check keeps nothing in the cache,
    // and its refusal is an error of evaluation.
    [Fact]
    public static void ApplicationOverload_OfTheSameName_LeavesALiteralToEvaluation()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddFunction("matches", (long subject, string pattern) => subject == 1)
            .Build()
            .GetEnvironmentOrThrow();

        CelCompileResult<bool> refused = environment.Compile<bool>("matches('abc', '(')");
        Assert.Equal(CelErrorCode.InvalidRegex, Assert.Single(refused.Errors).Code);
        Assert.Equal(1, environment.PatternCache.Count);

        CelProgram<bool> program = environment.Compile<bool>("matches(dyn('abc'), 'a**')").GetProgramOrThrow();
        Assert.Equal(1, environment.PatternCache.Count);

        CelError error = Assert.Single(program.Evaluate(new CelBindings(), null, CancellationToken.None).Errors);
        Assert.Equal((CelErrorCode.EvalInvalidRegex, 7), (error.Code, error.Location.Offset));
        Assert.Equal(2, environment.PatternCache.Count);

        CelProgram<bool> ofTheApplication = environment.Compile<bool>("matches(dyn(1), 'a**')").GetProgramOrThrow();
        Assert.True(ofTheApplication.Evaluate(new CelBindings(), null, CancellationToken.None).GetValueOrThrow());
    }

    private static CelEnvironment NewEnvironment() =>
        new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddVariable<string>("s")
            .AddVariable<string>("p")
            .Build()
            .GetEnvironmentOrThrow();

    private static CelEvalResult<bool> Evaluate(CelEnvironment environment, string expression, string subject, string pattern) =>
        environment.Compile<bool>(expression).GetProgramOrThrow().Evaluate(new CelBindings().Add("s", subject).Add("p", pattern), null, CancellationToken.None);
}

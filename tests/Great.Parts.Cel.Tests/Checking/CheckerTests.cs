using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Tests.Syntax;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Checking;

// The rules per node, the check errors and the overload candidates of type checking.
public sealed class CheckerTests
{
    // "<expression> → <type of the expression>" in the environment Declared.
    private const string Types = """
        null → null_type
        false → bool
        -7 → int
        7u → uint
        0.5 → double
        "text" → string
        b"\x01" → bytes
        n → int
        u → uint
        t → string
        any → dyn
        numbers → list(int)
        n + 2 → int
        -n → int
        n / n → int
        true || false → bool
        any + n → int
        any || any → bool
        -any → dyn
        [] → list(dyn)
        [3, n] → list(int)
        [3, 3u] → list(dyn)
        [any, 3] → list(dyn)
        [[3], numbers] → list(list(int))
        [[3], []] → list(list(int))
        {} → map(dyn, dyn)
        {"a": 3, "b": n} → map(string, int)
        {"a": 3, 4: "b"} → map(dyn, dyn)
        {3: "a", 4u: "b"} → map(dyn, string)
        {0.5: null} → map(double, null_type)
        [{}] → list(map(dyn, dyn))
        """;

    // "<expression> → <code> <offset> <line> <column> <message>".
    private const string Errors = """
        missing → 2000 0 1 1 undeclared reference to 'missing' (in container '')
        n +\n  other → 2000 6 2 3 undeclared reference to 'other' (in container '')
        unknown(n) → 2000 7 1 8 undeclared reference to 'unknown' (in container '')
        t + 2 → 2001 2 1 3 found no matching overload for '_+_' applied to '(string, int)'
        -u → 2001 0 1 1 found no matching overload for '-_' applied to '(uint)'
        n || true → 2002 0 1 1 expected type 'bool' but found 'int'
        numbers - [n] → 2001 8 1 9 found no matching overload for '_-_' applied to '(list(int), list(int))'
        {t: n} / 2 → 2001 7 1 8 found no matching overload for '_/_' applied to '(map(string, int), int)'
        null / n → 2001 5 1 6 found no matching overload for '_/_' applied to '(null_type, int)'
        """;

    private static readonly CelEnvironment Declared = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable("n", CelType.Int)
        .AddVariable("u", CelType.UInt)
        .AddVariable("t", CelType.String)
        .AddVariable("any", CelType.Dyn)
        .AddVariable("numbers", CelType.ListOf(CelType.Int))
        .Build()
        .GetEnvironmentOrThrow();

    public static TheoryData<string, string> TypeCases => CaseTable.Parse(Types);

    public static TheoryData<string, string> ErrorCases => CaseTable.Parse(Errors);

    [Theory]
    [MemberData(nameof(TypeCases))]
    public static void Expression_HasTheTypeOfItsRule(string expression, string type)
    {
        CheckResult result = Check(Declared, expression);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Errors);
        Assert.Equal(type, result.Expression.ResultType.ToString());
    }

    [Theory]
    [MemberData(nameof(ErrorCases))]
    public static void Error_HasTheMessageAndThePositionOfItsTable(string expression, string expected)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(expected);

        CheckResult result = Check(Declared, expression.Replace("\\n", "\n", StringComparison.Ordinal));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Expression);
        CelError error = Assert.Single(result.Errors);
        string[] parts = expected.Split(' ', 5);
        Assert.Equal(
            (parts[0], CelErrorStage.Check, parts[1], parts[2], parts[3], parts[4]),
            (((int)error.Code).ToString(CultureInfo.InvariantCulture), error.Stage, Text(error.Location.Offset), Text(error.Location.Line), Text(error.Location.Column), error.Message));
        Assert.Null(error.SourceLocation);
    }

    // A part with the type error makes its list, map or call have that type too, without a new error.
    [Theory]
    [InlineData("missing + 2")]
    [InlineData("-(missing / n)")]
    [InlineData("[missing, 3] + n")]
    [InlineData("{missing: 3}")]
    [InlineData("unknown(missing)")]
    public static void NodeWithTheTypeError_ProducesNoFurtherErrors(string expression)
    {
        CelError error = Assert.Single(Check(Declared, expression).Errors);

        Assert.Equal("undeclared reference to 'missing' (in container '')", error.Message);
    }

    [Fact]
    public static void Errors_AreAllReturnedInTheOrderOfTheirOffsets()
    {
        CheckResult result = Check(Declared, "first + (t + 2) + -second");

        Assert.Equal(
            [(CelErrorCode.UndeclaredReference, 0), (CelErrorCode.NoMatchingOverload, 11), (CelErrorCode.UndeclaredReference, 19)],
            result.Errors.Select(error => (error.Code, error.Location.Offset)));
    }

    [Fact]
    public static void References_NameTheVariableOfAnIdentifierAndTheCandidatesOfACall()
    {
        CheckResult result = Check(Declared, "n + n");

        Assert.True(result.IsSuccess);
        var call = (CallNode)result.Expression.Parsed.Root;
        Assert.All(
            call.Arguments,
            argument => Assert.Equal("n", result.Expression.References[argument.Id].Variable?.Name));
        Reference reference = result.Expression.References[call.Id];
        Assert.Null(reference.Variable);
        Assert.Equal(["add_int64"], reference.Candidates.Select(candidate => candidate.Declaration.OverloadId));
        Assert.Equal(
            [CelType.Int, CelType.Int, CelType.Int],
            new[] { call.Arguments[0].Id, call.Arguments[1].Id, call.Id }.Select(id => result.Expression.Types[id]));
    }

    // Overloads of one function f that accept dyn: all are candidates, in the order of the index.
    [Theory]
    [InlineData("f(any)", "f_int f_string f_list", "dyn")]
    [InlineData("f(1)", "f_int", "int")]
    [InlineData("f('a')", "f_string", "string")]
    [InlineData("g(any)", "g_int g_string", "bool")]
    public static void Candidates_AreTheOverloadsThatAcceptTheArgumentsInTheOrderOfTheIndex(string expression, string candidates, string type)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        CheckResult result = Checker.Check(Parse(expression), Index());

        Assert.True(result.IsSuccess);
        Assert.Equal(
            candidates.Split(' '),
            result.Expression.References[result.Expression.Parsed.Root.Id].Candidates.Select(candidate => candidate.Declaration.OverloadId));
        Assert.Equal(type, result.Expression.ResultType.ToString());
    }

    [Theory]
    [InlineData("f(1, 2)", "found no matching overload for 'f' applied to '(int, int)'")]
    [InlineData("f()", "found no matching overload for 'f' applied to '()'")]
    [InlineData("f(true)", "found no matching overload for 'f' applied to '(bool)'")]
    public static void KnownFunctionWithoutAFittingOverload_IsNoMatchingOverload(string expression, string message)
    {
        CelError error = Assert.Single(Checker.Check(Parse(expression), Index()).Errors);

        Assert.Equal((CelErrorCode.NoMatchingOverload, message), (error.Code, error.Message));
    }

    [Fact]
    public static void Check_OfNull_ThrowsArgumentNullException()
    {
        Assert.Equal("parsed", Assert.Throws<ArgumentNullException>(() => Declared.Check(null!)).ParamName);
    }

    [Fact]
    public static void Check_GivesTheResultTypeOfTheExpression()
    {
        CheckResult result = Check(Declared, "[n / 2]");

        Assert.True(result.IsSuccess);
        Assert.Equal(CelType.ListOf(CelType.Int), result.Expression.ResultType);
    }

    private static CheckResult Check(CelEnvironment environment, string expression) => environment.Check(Parse(expression));

    private static ParsedExpression Parse(string expression)
    {
        ParseResult parsed = Parser.Parse(expression);
        Assert.True(parsed.IsSuccess, expression);
        return parsed.Expression;
    }

    private static string Text(int number) => number.ToString(CultureInfo.InvariantCulture);

    // The variable any of type dyn, and the functions f and g with overloads in this order.
    private static DeclarationIndex Index() =>
        DeclarationIndex.Create(
            [
                Overload("f", "f_int", CelType.Int, CelType.Int),
                Overload("f", "f_string", CelType.String, CelType.String),
                Overload("f", "f_list", CelType.ListOf(CelType.Dyn), CelType.Bool),
                Overload("g", "g_int", CelType.Int, CelType.Bool),
                Overload("g", "g_string", CelType.String, CelType.Bool),
            ],
            [new CelVariableDecl("any", CelType.Dyn)]);

    private static OverloadBinding Overload(string function, string overloadId, CelType parameterType, CelType resultType) =>
        new(function, new CelOverloadDecl(overloadId, false, ImmutableArray.Create(parameterType), resultType), null, 1, null, "test");
}

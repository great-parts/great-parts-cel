using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Tests.Syntax;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Evaluation;

// Evaluation of checked plans: reads of variables, strict calls and literals, chains of || and the
// errors of evaluation with their positions.
public sealed class EvaluationTests
{
    // "<expression> → <code> <offset> <message>" on the bindings Bound: n, flag, c and d have no value.
    private const string Errors = """
        n → 3009 0 no value is bound to variable 'n'
        1 + n → 3009 4 no value is bound to variable 'n'
        n + (1 / 0) → 3009 0 no value is bound to variable 'n'
        (1 / 0) + n → 3004 3 division by zero
        -(1 / 0) → 3004 4 division by zero
        [1, n, 1 / 0] → 3009 4 no value is bound to variable 'n'
        [1 / 0, n] → 3004 3 division by zero
        {n: 1, 2: 1 / 0} → 3009 1 no value is bound to variable 'n'
        {1: 1 / 0, n: 2} → 3004 6 division by zero
        {1.0: 1, 2: n} → 3009 12 no value is bound to variable 'n'
        {1: 'a', 1u: 'b', 1.0: 'c'} → 3027 18 unsupported map key type 'double'
        {2: 1, 2: 2, b"x": 3} → 3027 13 unsupported map key type 'bytes'
        {null: false} → 3027 1 unsupported map key type 'null_type'
        {[1]: 2} → 3027 1 unsupported map key type 'list'
        {1: 2, {}: 3} → 3027 7 unsupported map key type 'map'
        {1: 'a', 1u: 'b'} → 3010 0 duplicate map key: 1u
        {'k': 1, 'k': 2} → 3010 0 duplicate map key: "k"
        {'a"b': 1, 'a"b': 2} → 3010 0 duplicate map key: "a\"b"
        {'a\nb': 1, 'a\nb': 2} → 3010 0 duplicate map key: "a\nb"
        {'\\\r\t\x01\x7f é': 1, '\\\r\t\x01\x7f é': 2} → 3010 0 duplicate map key: "\\\r\t\u0001\u007F é"
        {true: 1, true: 3} → 3010 0 duplicate map key: true
        {-1: 1, 2: 2, -1: 3} → 3010 0 duplicate map key: -1
        flag || false → 3009 0 no value is bound to variable 'flag'
        false || flag → 3009 9 no value is bound to variable 'flag'
        a || flag || false → 3009 5 no value is bound to variable 'flag'
        flag || any → 3009 0 no value is bound to variable 'flag'
        any || false → 3000 4 no such overload: _||_(int, bool)
        any || flag → 3000 4 no such overload: _||_(int, error)
        false || any || a → 3000 13 no such overload: _||_(bool, int, bool)
        a || s || any || a → 3000 7 no such overload: _||_(bool, string, int, bool)
        s + 1 → 3000 2 no such overload: _+_(string, int)
        -s → 3000 0 no such overload: -_(string)
        """;

    // a is false, b is true, any holds 1 and s holds "s".
    private static readonly IReadOnlyDictionary<string, CelValue> Bound = new Dictionary<string, CelValue>
    {
        ["a"] = CelValue.FromBoolean(false),
        ["b"] = CelValue.FromBoolean(true),
        ["any"] = CelValue.FromInt64(1),
        ["s"] = CelValue.FromString("s"),
    };

    public static TheoryData<string, string> ErrorCases => CaseTable.Parse(Errors);

    [Theory]
    [MemberData(nameof(ErrorCases))]
    public static void Error_HasItsCodePositionAndMessage(string expression, string expected)
    {
        ArgumentNullException.ThrowIfNull(expected);

        CelError error = Plans.Error(Plans.Checked(expression).EvaluateOn(Bound));

        string[] parts = expected.Split(' ', 3);
        Assert.Equal(
            (parts[0], parts[1], parts[2]),
            (((int)error.Code).ToString(CultureInfo.InvariantCulture), error.Location.Offset.ToString(CultureInfo.InvariantCulture), error.Message));
    }

    [Fact]
    public static void Error_HasTheLineAndTheColumnOfItsOffset()
    {
        CelError error = Plans.Error(Plans.Checked("1 +\n  n").EvaluateOn(Bound));

        Assert.Equal((6, 2, 3), (error.Location.Offset, error.Location.Line, error.Location.Column));
    }

    // A true operand absorbs errors and operands that are not bool, before or after it.
    [Theory]
    [InlineData("b || flag")]
    [InlineData("flag || b")]
    [InlineData("flag || a || b")]
    [InlineData("any || b")]
    [InlineData("s || flag || a || b")]
    [InlineData("a || (flag || b)")]
    public static void Or_WithATrueOperand_IsTrue(string expression)
    {
        Assert.Equal(CelValue.FromBoolean(true), Plans.Checked(expression).EvaluateOn(Bound));
    }

    [Fact]
    public static void Or_OfFalseOperands_IsFalse()
    {
        Assert.Equal(CelValue.FromBoolean(false), Plans.Checked("a || false || a").EvaluateOn(Bound));
    }

    // The operands after the first true are not evaluated, so their variables are not read.
    [Fact]
    public static void Or_StopsAtTheFirstTrueOperand()
    {
        var bindings = new CountingBindings(new() { ["a"] = CelValue.FromBoolean(false), ["b"] = CelValue.FromBoolean(true) });

        CelValue value = Plans.Checked("a || b || c || d").EvaluateOn(bindings);

        Assert.Equal((CelValue.FromBoolean(true), 2), (value, bindings.Lookups));
    }

    // Every identifier of n reads one slot: the bindings are asked once per evaluation.
    [Fact]
    public static void Variable_IsReadFromTheBindingsOncePerEvaluation()
    {
        Plan plan = Plans.Checked("n + -n + n / n");
        var first = new CountingBindings(new() { ["n"] = CelValue.FromInt64(3) });
        var second = new CountingBindings(new() { ["n"] = CelValue.FromInt64(5) });

        Assert.Equal(CelValue.FromInt64(1), plan.EvaluateOn(first));
        Assert.Equal(CelValue.FromInt64(1), plan.EvaluateOn(second));
        Assert.Equal((1, 1), (first.Lookups, second.Lookups));
    }

    // A call with an argument that is an error value does not run its implementation.
    [Fact]
    public static void ErrorArgument_IsTheValueOfTheCallWithoutRunningTheImplementation()
    {
        int calls = 0;
        Plan plan = Plans.CheckedWith(
            "[f(n), f(n / 0)]",
            Plans.Overload("f", "f_int", CelType.Int, (ReadOnlySpan<CelValue> _, in CallSite _) => CelValue.FromInt64(++calls)));

        CelError error = Plans.Error(plan.EvaluateOn(new Dictionary<string, CelValue> { ["n"] = CelValue.FromInt64(1) }));

        Assert.Equal((CelErrorCode.DivisionByZero, 11, 1), (error.Code, error.Location.Offset, calls));
    }

    // An argument of the static type dyn is checked against the parameter types before the call, so an
    // implementation is never called with a value of another kind.
    [Fact]
    public static void DynArgumentOfAnotherKind_IsEvalNoMatchingOverloadAtTheCall()
    {
        Plan plan = Plans.CheckedWith("g(any)", Plans.Overload("g", "g_int", CelType.Int, Plans.Constant));

        CelError error = Plans.Error(plan.EvaluateOn(new Dictionary<string, CelValue> { ["any"] = CelValue.FromString("s") }));

        Assert.Equal(
            (CelErrorCode.EvalNoMatchingOverload, 1, "no such overload: g(string)"),
            (error.Code, error.Location.Offset, error.Message));
        Assert.Equal(CelValue.FromInt64(42), plan.EvaluateOn(new Dictionary<string, CelValue> { ["any"] = CelValue.FromInt64(5) }));
    }

    // A dispatch calls the first candidate whose parameter types fit the kinds of the values.
    [Theory]
    [InlineData("int", "int")]
    [InlineData("string", "dyn")]
    [InlineData("bool", "dyn")]
    public static void Dispatch_CallsTheFirstCandidateThatFits(string kind, string expected)
    {
        Plan plan = Plans.CheckedWith(
            "h(any)",
            Plans.Overload("h", "h_int", CelType.Int, (ReadOnlySpan<CelValue> _, in CallSite _) => CelValue.FromString("int")),
            Plans.Overload("h", "h_dyn", CelType.Dyn, (ReadOnlySpan<CelValue> _, in CallSite _) => CelValue.FromString("dyn")));

        Assert.Equal(CelValue.FromString(expected), plan.EvaluateOn(new Dictionary<string, CelValue> { ["any"] = Value(kind) }));
    }

    [Fact]
    public static void DispatchWithoutAFittingCandidate_IsEvalNoMatchingOverloadAtTheCall()
    {
        Plan plan = Plans.CheckedWith(
            "h(any)",
            Plans.Overload("h", "h_int", CelType.Int, Plans.Constant),
            Plans.Overload("h", "h_string", CelType.String, Plans.Constant));

        CelError error = Plans.Error(plan.EvaluateOn(new Dictionary<string, CelValue> { ["any"] = CelValue.FromBoolean(true) }));

        Assert.Equal(
            (CelErrorCode.EvalNoMatchingOverload, 1, "no such overload: h(bool)"),
            (error.Code, error.Location.Offset, error.Message));
    }

    [Fact]
    public static void ListLiteral_HasItsElementsInOrder()
    {
        Assert.True(Plans.Checked("[n + 1, 'a', any]").EvaluateOn(new Dictionary<string, CelValue>
        {
            ["n"] = CelValue.FromInt64(1),
            ["any"] = CelValue.FromBoolean(true),
        }).TryGetList(out CelList? list));

        Assert.Equal([CelValue.FromInt64(2), CelValue.FromString("a"), CelValue.FromBoolean(true)], list);
    }

    [Fact]
    public static void MapLiteral_HasItsEntriesInTheOrderOfTheText()
    {
        Assert.True(Plans.Checked("{'b': 1, 2: 'x', 'a': -n}").EvaluateOn(new Dictionary<string, CelValue>
        {
            ["n"] = CelValue.FromInt64(3),
        }).TryGetMap(out CelMap? map));

        Assert.Equal(
            [
                (CelValue.FromString("b"), CelValue.FromInt64(1)),
                (CelValue.FromInt64(2), CelValue.FromString("x")),
                (CelValue.FromString("a"), CelValue.FromInt64(-3)),
            ],
            map.Select(entry => (entry.Key, entry.Value)));
    }

    // A value of the kind int, string or bool.
    private static CelValue Value(string kind) => kind switch
    {
        "int" => CelValue.FromInt64(1),
        "string" => CelValue.FromString("s"),
        _ => CelValue.FromBoolean(true),
    };
}

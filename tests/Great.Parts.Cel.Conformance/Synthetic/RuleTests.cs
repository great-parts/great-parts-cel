using System;
using System.Collections.Generic;
using Cel.Expr;
using Cel.Expr.Conformance.Test;
using Great.Parts.Cel.Conformance.Runner;
using Xunit;
using Type = Cel.Expr.Type;

namespace Great.Parts.Cel.Conformance.Synthetic;

// The rule of the cel-go v0.32.0 conformance runner, one test per step of the table "The rule" of docs/design/conformance.md.
// The outcomes are scripted: each test gives the rule what an evaluator would give.
public sealed class RuleTests
{
    private static readonly Type IntType = new() { Primitive = Type.Types.PrimitiveType.Int64 };
    private static readonly Type DynType = new() { Dyn = new() };
    private static readonly Expectation IntTwo = Matcher(ExpectationKind.Value, "value", Int(2));

    // ---- matcher ----

    [Fact]
    public void Matcher_CaseWithoutMatcher_ExpectsTrue()
    {
        SimpleTestFile file = new()
        {
            Name = "f",
            Section = { new SimpleTestSection { Name = "s", Test = { new SimpleTest { Name = "t", Expr = "x" } } } },
        };
        ConformanceCase testCase = Assert.Single(CaseReader.Read("f.textproto", file, "section { test { } }"));

        Assert.Equal(Judgement.Pass, Rule.Judge(testCase, CaseOutcome.Evaluated(DynType, new Value { BoolValue = true })));
        Assert.Equal(Judgement.Fail("expected bool true, got bool false"), Rule.Judge(testCase, CaseOutcome.Evaluated(DynType, new Value { BoolValue = false })));
    }

    // ---- parse, environment, check, program ----

    [Fact]
    public void Parse_Error_Fails()
    {
        Judgement judgement = Rule.Judge(Case(IntTwo), CaseOutcome.Failed(CaseStage.Parse, "Syntax error: mismatched input", "second"));

        Assert.Equal(Judgement.Fail("parse failed: 'Syntax error: mismatched input', 'second'"), judgement);
    }

    [Fact]
    public void Environment_ErrorOrThrowingBuilderMember_Fails()
    {
        Assert.Equal(
            Judgement.Fail("environment failed: 'unknown type'"),
            Rule.Judge(Case(IntTwo), CaseOutcome.Failed(CaseStage.Environment, "unknown type")));
        Assert.Equal(
            Judgement.Fail("environment threw NotImplementedException: 'The method or operation is not implemented.'"),
            Rule.Judge(Case(IntTwo), CaseOutcome.Threw(CaseStage.Environment, new NotImplementedException())));
    }

    [Fact]
    public void Check_Error_FailsEvenWhenAnEvaluationErrorIsExpected()
    {
        Expectation evaluationError = Matcher(ExpectationKind.EvaluationError, "eval_error", null);

        Judgement judgement = Rule.Judge(Case(evaluationError), CaseOutcome.Failed(CaseStage.Check, "found no matching overload for '_+_'"));

        Assert.Equal(Judgement.Fail("check failed: 'found no matching overload for \\'_+_\\''"), judgement);
    }

    [Fact]
    public void Program_ExceptionOfCompile_Fails()
    {
        Judgement judgement = Rule.Judge(Case(IntTwo), CaseOutcome.Threw(CaseStage.Check, new InvalidOperationException("line 1\nline 2")));

        Assert.Equal(Judgement.Fail("check threw InvalidOperationException: 'line 1\\u000aline 2'"), judgement);
    }

    // ---- check_only ----

    [Fact]
    public void CheckOnly_ComparesOnlyTheDeducedType()
    {
        Expectation typed = Matcher(ExpectationKind.TypedResult, "typed_result", Int(5), IntType);

        Assert.Equal(Judgement.Pass, Rule.Judge(Case(typed, CheckMode.CheckOnly), CaseOutcome.Checked(IntType)));
        Assert.Equal(Judgement.Fail("expected type int, got type dyn"), Rule.Judge(Case(typed, CheckMode.CheckOnly), CaseOutcome.Checked(DynType)));
    }

    [Fact]
    public void CheckOnly_MatcherOtherThanTypedResult_Fails()
    {
        Judgement judgement = Rule.Judge(Case(IntTwo, CheckMode.CheckOnly), CaseOutcome.Checked(IntType));

        Assert.Equal(Judgement.Fail("a check_only case needs the matcher typed_result, not value"), judgement);
    }

    // ---- bindings ----

    [Theory]
    [InlineData(ExprValue.KindOneofCase.Value, "")]
    [InlineData(ExprValue.KindOneofCase.Error, "the binding 'x' is not a value but an error; the runner binds values only")]
    [InlineData(ExprValue.KindOneofCase.Unknown, "the binding 'x' is not a value but an unknown set; the runner binds values only")]
    public static void Bindings_OtherThanValue_Fail(ExprValue.KindOneofCase kind, string reason)
    {
        ExprValue binding = kind switch
        {
            ExprValue.KindOneofCase.Value => new ExprValue { Value = Int(1) },
            ExprValue.KindOneofCase.Error => new ExprValue { Error = new ErrorSet() },
            _ => new ExprValue { Unknown = new UnknownSet() },
        };
        ConformanceCase testCase = Case(IntTwo) with { Bindings = new Dictionary<string, ExprValue>(StringComparer.Ordinal) { ["x"] = binding } };

        Judgement judgement = Rule.Judge(testCase, CaseOutcome.Evaluated(IntType, Int(2)));

        Assert.Equal(new Judgement(kind == ExprValue.KindOneofCase.Value, reason), judgement);
    }

    // ---- value ----

    [Fact]
    public void Value_EqualValue_Passes()
    {
        Assert.Equal(Judgement.Pass, Rule.Judge(Case(IntTwo), CaseOutcome.Evaluated(DynType, Int(2))));
    }

    [Fact]
    public void Value_OtherValueOrEvaluationError_Fails()
    {
        Assert.Equal(Judgement.Fail("expected int 2, got uint 2"), Rule.Judge(Case(IntTwo), CaseOutcome.Evaluated(IntType, new Value { Uint64Value = 2 })));
        Assert.Equal(
            Judgement.Fail("expected int 2, got error 'no such overload'"),
            Rule.Judge(Case(IntTwo), CaseOutcome.EvaluationFailed(IntType, "no such overload")));
        Assert.Equal(
            Judgement.Fail("expected int 2, got no value after the stage check"),
            Rule.Judge(Case(IntTwo), CaseOutcome.Checked(IntType)));
    }

    [Theory]
    [InlineData(0.0, -0.0, true)]
    [InlineData(1.5, 1.5, true)]
    [InlineData(double.NaN, double.NaN, false)]
    [InlineData(1.0, 1.0000000000000002, false)]
    public static void Value_DoublesAreComparedWithGoEquality(double expected, double actual, bool equal)
    {
        Assert.Equal(equal, Rule.AreEqual(new Value { DoubleValue = expected }, new Value { DoubleValue = actual }));
    }

    [Fact]
    public void Value_MapEntriesInAnyOrder_AreEqual_ListElementsInOrderOnly()
    {
        Value map = Map((Int(1), Int(10)), (Int(2), Int(20)));

        Assert.True(Rule.AreEqual(map, Map((Int(2), Int(20)), (Int(1), Int(10)))));
        Assert.False(Rule.AreEqual(map, Map((Int(1), Int(20)), (Int(2), Int(10)))));
        Assert.False(Rule.AreEqual(map, Map((Int(1), Int(10)), (Int(1), Int(10)))));
        Assert.False(Rule.AreEqual(map, Map((Int(1), Int(10)))));
        Assert.True(Rule.AreEqual(List(Int(1), new Value { DoubleValue = -0.0 }), List(Int(1), new Value { DoubleValue = 0.0 })));
        Assert.False(Rule.AreEqual(List(Int(1), Int(2)), List(Int(2), Int(1))));
        Assert.False(Rule.AreEqual(List(Int(1)), List(Int(1), Int(1))));
    }

    // ---- typed_result ----

    [Fact]
    public void TypedResult_ComparesTheValueAndTheDeducedType()
    {
        Expectation typed = Matcher(ExpectationKind.TypedResult, "typed_result", Int(2), IntType);

        Assert.Equal(Judgement.Pass, Rule.Judge(Case(typed), CaseOutcome.Evaluated(IntType, Int(2))));
        Assert.Equal(Judgement.Fail("expected type int, got type dyn"), Rule.Judge(Case(typed), CaseOutcome.Evaluated(DynType, Int(2))));
        Assert.Equal(Judgement.Fail("expected int 2, got int 3"), Rule.Judge(Case(typed), CaseOutcome.Evaluated(IntType, Int(3))));
    }

    // ---- eval_error ----

    [Fact]
    public void EvalError_AnyEvaluationErrorPasses_AValueOrAnExceptionFails()
    {
        Expectation evaluationError = Matcher(ExpectationKind.EvaluationError, "eval_error", null);

        Assert.Equal(Judgement.Pass, Rule.Judge(Case(evaluationError), CaseOutcome.EvaluationFailed(IntType, "another message")));
        Assert.Equal(Judgement.Fail("expected an evaluation error, got int 2"), Rule.Judge(Case(evaluationError), CaseOutcome.Evaluated(IntType, Int(2))));
        Assert.Equal(
            Judgement.Fail("evaluation threw OverflowException: 'Arithmetic operation resulted in an overflow.'"),
            Rule.Judge(Case(evaluationError), CaseOutcome.Threw(CaseStage.Evaluate, new OverflowException())));
    }

    // ---- other matchers ----

    [Theory]
    [InlineData("any_eval_errors")]
    [InlineData("unknown")]
    [InlineData("any_unknowns")]
    public static void OtherMatchers_Fail(string matcher)
    {
        Judgement judgement = Rule.Judge(Case(Matcher(ExpectationKind.Other, matcher, null)), CaseOutcome.EvaluationFailed(IntType, "error"));

        Assert.Equal(Judgement.Fail($"the matcher {matcher} is not supported"), judgement);
    }

    private static ConformanceCase Case(Expectation expected, CheckMode check = CheckMode.Check) =>
        new("f/s/t", "f.textproto", 1, 1, "x", new CaseEnvironment(string.Empty, [], false), new Dictionary<string, ExprValue>(), check, expected);

    private static Expectation Matcher(ExpectationKind kind, string matcher, Value? value, Type? type = null) => new(kind, matcher, value, type);

    private static Value Int(long value) => new() { Int64Value = value };

    private static Value List(params Value[] values) => new() { ListValue = new ListValue { Values = { values } } };

    private static Value Map(params (Value Key, Value Value)[] entries)
    {
        MapValue map = new();
        foreach ((Value key, Value value) in entries)
        {
            map.Entries.Add(new MapValue.Types.Entry { Key = key, Value = value });
        }

        return new Value { MapValue = map };
    }
}

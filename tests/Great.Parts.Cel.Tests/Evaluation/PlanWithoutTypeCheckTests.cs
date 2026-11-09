using System.Collections.Generic;
using System.Linq;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Evaluation;

// Plans of parsed expressions without type checking: identifiers read names from the bindings, and
// calls choose among all overloads of their name and argument count.
public sealed class PlanWithoutTypeCheckTests
{
    private static readonly IReadOnlyDictionary<string, CelValue> NoBindings = new Dictionary<string, CelValue>();

    [Fact]
    public static void NameWithoutAValue_IsUnboundAtItsIdentifier()
    {
        CelError error = Plans.Error(Plans.WithoutTypeCheck("1 + x").EvaluateOn(NoBindings));

        Assert.Equal(
            (CelErrorCode.Unbound, 4, "no value is bound to variable 'x'"),
            (error.Code, error.Location.Offset, error.Message));
    }

    // A bound name has its value, declared in the environment or not.
    [Fact]
    public static void BoundName_HasItsValueWithoutADeclaration()
    {
        CelValue value = Plans.WithoutTypeCheck("undeclared").EvaluateOn(new Dictionary<string, CelValue> { ["undeclared"] = CelValue.FromString("v") });

        Assert.Equal(CelValue.FromString("v"), value);
    }

    [Fact]
    public static void UnknownFunction_IsADispatchWithoutCandidates()
    {
        var dispatch = Assert.IsType<DispatchPlanNode>(Plans.WithoutTypeCheck("f_unknown(17)").Root);

        Assert.Empty(dispatch.Candidates);
        Assert.Equal(9, dispatch.Offset);
    }

    // The arguments are evaluated first: without an error among them, no candidate fits.
    [Theory]
    [InlineData("f_unknown(17)", CelErrorCode.EvalNoMatchingOverload, 9, "no such overload: f_unknown(int)")]
    [InlineData("f_unknown(x)", CelErrorCode.Unbound, 10, "no value is bound to variable 'x'")]
    [InlineData("n(1, 2)", CelErrorCode.EvalNoMatchingOverload, 1, "no such overload: n(int, int)")]
    [InlineData("1 + 'a'", CelErrorCode.EvalNoMatchingOverload, 2, "no such overload: _+_(int, string)")]
    public static void CallWithoutAFittingOverload_IsEvalNoMatchingOverloadAtTheCall(string expression, CelErrorCode code, int offset, string message)
    {
        CelError error = Plans.Error(Plans.WithoutTypeCheck(expression).EvaluateOn(NoBindings));

        Assert.Equal((code, offset, message), (error.Code, error.Location.Offset, error.Message));
    }

    [Fact]
    public static void Call_DispatchesAmongAllOverloadsOfItsNameAndArgumentCount()
    {
        Plan plan = Plans.WithoutTypeCheck("1 + 2");

        var dispatch = Assert.IsType<DispatchPlanNode>(plan.Root);
        Assert.Equal(["add_bytes", "add_double", "add_duration_duration", "add_duration_timestamp", "add_timestamp_duration", "add_int64", "add_list", "add_string", "add_uint64"], dispatch.Candidates.Select(candidate => candidate.Declaration.OverloadId));
        Assert.Equal(CelValue.FromInt64(3), plan.EvaluateOn(NoBindings));
    }

    [Fact]
    public static void Or_IsAChainThatAbsorbsTheErrorOfAnUnboundName()
    {
        Plan plan = Plans.WithoutTypeCheck("x || y || true");

        Assert.Equal(3, Assert.IsType<OrPlanNode>(plan.Root).Operands.Length);
        Assert.Equal(CelValue.FromBoolean(true), plan.EvaluateOn(NoBindings));
    }
}

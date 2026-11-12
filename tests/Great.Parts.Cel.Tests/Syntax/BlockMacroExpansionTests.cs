using System.Globalization;
using System.Linq;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Tests.Examples.Library;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Syntax;

// The macros of the internal set block where its examples stop: the notation of a number, a number that is no int, the macros of
// two iteration variables with the names the set makes, a list with an optional element, and the expressions without type checking.
public sealed class BlockMacroExpansionTests
{
    private readonly CelEnvironment runner = BlockExample.Builder()
        .AddExtension(CelExtensions.TwoVarComprehensions(0))
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelEnvironment application = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .Build()
        .GetEnvironmentOrThrow();

    // The digits of the number are those of its value, whatever the notation of the literal: 007 is @index7 and 0x10 is @index16.
    [Fact]
    public void Number_IsReadByItsValue_NotByItsNotation()
    {
        string seventeen = string.Join(", ", Enumerable.Range(0, 17).Select(slot => slot.ToString(CultureInfo.InvariantCulture)));

        Assert.Empty(ExtensionRows.CheckValues(
            runner,
            "cel.block([0, 1, 2, 3, 4, 5, 6, 7], cel.index(007)) => 7\ncel.block([" + seventeen + "], cel.index(0x10)) => 16"));
    }

    // A literal beyond int64 is no number of the macro: the parser refuses the literal first, and the macro sees no int.
    [Fact]
    public void NumberBeyondInt64_IsInvalidLiteralFirst()
    {
        CelCompileResult<CelValue> compiled = runner.Compile<CelValue>("cel.index(9223372036854775808)");

        Assert.False(compiled.IsSuccess);
        Assert.Equal(CelErrorCode.InvalidLiteral, compiled.Errors[0].Code);
    }

    // The names that the macros make are never the name of the accumulator, so only a name written in the text is refused as an iteration
    // variable, in the environment of the runner as in one without the set.
    [Fact]
    public void IterationVariableNamedAsTheAccumulator_IsRefusedWithAndWithoutTheSet()
    {
        CelError inRunner = Assert.Single(runner.Compile<CelValue>("[1, 2].map(__result__, __result__)").Errors);
        CelError inApplication = Assert.Single(application.Compile<CelValue>("[1, 2].map(__result__, __result__)").Errors);

        Assert.Equal(CelErrorCode.InvalidMacroCall, inRunner.Code);
        Assert.Equal("invalid call of macro 'map': iteration variable overwrites accumulator variable", inRunner.Message);
        Assert.Equal((inApplication.Code, inApplication.Message, inApplication.Location.Offset), (inRunner.Code, inRunner.Message, inRunner.Location.Offset));
    }

    // cel.iterVar makes identifiers, which the macros of two variables take as the names of the index and the element.
    [Fact]
    public void IterVar_NamesTheVariablesOfAMacroOfTwoVariables()
    {
        Assert.Empty(ExtensionRows.CheckValues(
            runner,
            "[10, 20].transformList(cel.iterVar(0, 0), cel.iterVar(0, 1), cel.iterVar(0, 0) + cel.iterVar(0, 1)) => [10, 21]\n"
            + "[10, 20].all(cel.iterVar(0, 0), cel.iterVar(0, 1), cel.iterVar(0, 1) > cel.iterVar(0, 0)) => true"));
    }

    // Two equal names are the error of the macro of two variables, at the second name: the macro sees the identifiers, not the calls.
    [Fact]
    public void IterVar_OfTheSameNumbersTwice_IsADuplicateName()
    {
        Assert.Empty(ExtensionRows.CheckErrors(
            runner,
            "Parse | [1].transformList(cel.iterVar(0, 0), cel.iterVar^(0, 0), 1) | InvalidMacroCall | invalid call of macro 'transformList': duplicate variable name: @it:0:0"));
    }

    // The macro takes any list literal; the list with an optional element is refused after the traversal by the check of cel.@block.
    [Fact]
    public void ListWithAnOptionalElement_PassesTheMacro_AndFailsTheCheck()
    {
        CelCompileResult<CelValue> compiled = runner.Compile<CelValue>("cel.block([?optional.of(1)], 1)");

        Assert.False(compiled.IsSuccess);
        CelError error = Assert.Single(compiled.Errors);
        Assert.Equal((CelErrorCode.NoMatchingOverload, CelErrorStage.Check), (error.Code, error.Stage));
    }

    // Without type checking the macros expand just the same and the slots are read by the planner: an index outside a block is unbound.
    [Fact]
    public void Expression_WithoutTypeCheck_IsPlannedAsABlock()
    {
        Assert.Equal(CelValue.FromInt64(2), EvaluateUnchecked("cel.block([1, cel.index(0) + 1], cel.index(1))").GetValueOrThrow());

        CelError error = Assert.Single(EvaluateUnchecked("cel.index(3)").Errors);
        Assert.Equal((CelErrorCode.Unbound, "no value is bound to variable '@index3'"), (error.Code, error.Message));
    }

    private CelEvalResult<CelValue> EvaluateUnchecked(string expression)
    {
        ParseResult parsed = runner.Parse(expression);
        Assert.True(parsed.IsSuccess, string.Join("; ", parsed.Errors.Select(error => error.Message)));
        return runner.EvaluateWithoutTypeCheck(parsed.Expression, new CelBindings());
    }
}

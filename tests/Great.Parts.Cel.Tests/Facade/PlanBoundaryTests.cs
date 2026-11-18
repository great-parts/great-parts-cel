using System;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Syntax;
using Xunit;

namespace Great.Parts.Cel.Tests.Facade;

// The compilation boundary of planning (docs/design/facade.md, "Compilation boundary"): an unexpected exception during
// planning becomes InternalError of the stage Check with the exception; OutOfMemoryException passes.
public sealed class PlanBoundaryTests
{
    private static readonly CelEnvironment TestEnvironment = new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();

    private static Planner PlannerOf(string expression)
    {
        ParseResult parsed = TestEnvironment.Parse(expression);
        CheckResult checkedResult = TestEnvironment.Check(parsed.Expression!);
        Assert.True(checkedResult.IsSuccess, expression);
        return Planner.For(checkedResult.Expression, TestEnvironment.Declarations, TestEnvironment.TimeZones);
    }

    // Before the first node the error is at the root of the expression, here the offset of the "+" on the second line.
    [Fact]
    public static void ExceptionInPlanning_IsInternalErrorOfStageCheckWithTheException()
    {
        var exception = new InvalidOperationException("defect");

        bool planned = CelEnvironment.TryPlanWithinBoundary(PlannerOf("1\n+ 2"), _ => throw exception, out Plan? plan, out CelError? error);

        Assert.False(planned);
        Assert.Null(plan);
        Assert.NotNull(error);
        Assert.Equal(
            (CelErrorCode.InternalError, CelErrorStage.Check, "internal error during planning; the original exception is in CelError.Exception", 2, 2, 1),
            (error.Code, error.Stage, error.Message, error.Location.Offset, error.Location.Line, error.Location.Column));
        Assert.Same(exception, error.Exception);
    }

    // InsufficientMemoryException is an OutOfMemoryException that code can throw.
    [Fact]
    public static void OutOfMemoryException_IsNotCaught()
    {
        Assert.Throws<InsufficientMemoryException>(
            () => CelEnvironment.TryPlanWithinBoundary(PlannerOf("1 + 2"), _ => throw new InsufficientMemoryException(), out _, out _));
    }

    // Without an exception the boundary gives the plan of the step as it is.
    [Fact]
    public static void PlanningWithoutException_GivesThePlan()
    {
        bool planned = CelEnvironment.TryPlanWithinBoundary(PlannerOf("1 + 2"), static planner => planner.Run(), out Plan? plan, out CelError? error);

        Assert.True(planned);
        Assert.NotNull(plan);
        Assert.Null(error);
    }
}

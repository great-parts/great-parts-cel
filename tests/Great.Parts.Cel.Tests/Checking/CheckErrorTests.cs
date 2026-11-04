using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Checking;

// TypeMismatch, ExpectedTypeMismatch, InsufficientStack and InternalError of the stage Check, with their
// texts and places, and the order of the list of errors.
public sealed class CheckErrorTests
{
    private static readonly DeclarationIndex Declared = CheckingIndex.Create(
        [],
        [
            new CelVariableDecl("n", CelType.Int),
            new CelVariableDecl("s", CelType.String),
            new CelVariableDecl("any", CelType.Dyn),
            new CelVariableDecl("nums", CelType.ListOf(CelType.Int)),
        ]);

    // An operand of && or || that is not assignable to bool, also the predicate of all and exists, which the
    // expansion makes an operand of the step.
    [Theory]
    [InlineData("n && true", 0, "int")]
    [InlineData("true || s", 8, "string")]
    [InlineData("any && nums", 7, "list(int)")]
    [InlineData("nums.exists(x, x)", 15, "int")]
    [InlineData("nums.all(x,\n  'a')", 14, "string")]
    public static void OperandThatIsNotBool_IsTypeMismatchAtTheOperand(string expression, int offset, string actual)
    {
        CelError error = Assert.Single(Check(expression).Errors);

        Assert.Equal((CelErrorCode.TypeMismatch, CelErrorStage.Check), (error.Code, error.Stage));
        Assert.Equal("expected type 'bool' but found '" + actual + "'", error.Message);
        Assert.Equal(offset, error.Location.Offset);
    }

    [Fact]
    public static void BothOperandsThatAreNotBool_AreTwoErrors()
    {
        CheckResult result = Check("1 || 'a'");

        Assert.Equal(
            [(CelErrorCode.TypeMismatch, 0, "expected type 'bool' but found 'int'"), (CelErrorCode.TypeMismatch, 5, "expected type 'bool' but found 'string'")],
            result.Errors.Select(error => (error.Code, error.Location.Offset, error.Message)));
    }

    // A loop condition that is not bool and a loop step that is not of the type of the accumulator, in a
    // comprehension that no macro creates.
    [Fact]
    public static void LoopConditionAndStepOfTheWrongTypes_AreTypeMismatchAtEach()
    {
        var nodes = new NodeFactory();
        ExprNode range = nodes.Ident("nums", 0);
        ExprNode init = nodes.Constant(CelValue.FromInt64(0), 5);
        ExprNode condition = nodes.Constant(CelValue.FromString("c"), 8);
        ExprNode step = nodes.Ident("s", 12);
        ExprNode result = nodes.Ident("acc", 14);
        ExprNode root = nodes.Comprehension("x", range, "acc", init, condition, step, result, 4);
        ParsedExpression parsed = nodes.Build(root, new LineTable("nums(0, 'c', s, acc)"));

        CheckResult checkedResult = Checker.Check(parsed, Declared);

        Assert.Equal(
            [(8, "expected type 'bool' but found 'string'"), (12, "expected type 'int' but found 'string'")],
            checkedResult.Errors.Select(error => (error.Location.Offset, error.Message)));
        Assert.All(checkedResult.Errors, error => Assert.Equal(CelErrorCode.TypeMismatch, error.Code));
    }

    // The list is ordered by offset; errors at one offset keep the order in which the checker found them.
    [Fact]
    public static void Errors_AreOrderedByOffsetAndStableAtOneOffset()
    {
        var nodes = new NodeFactory();
        ExprNode second = nodes.Ident("second", 6);
        ExprNode third = nodes.Ident("third", 6);
        ExprNode first = nodes.Ident("first", 1);
        ParsedExpression parsed = nodes.Build(nodes.List([second, third, first], [], 0), new LineTable("[first, …]"));

        CheckResult result = Checker.Check(parsed, Declared);

        Assert.Equal(
            ["first", "second", "third"],
            result.Errors.Select(error => error.Message.Split('\'')[1]));
    }

    [Fact]
    public static void CallAroundAnInvalidRange_IsReportedBeforeTheRange()
    {
        CheckResult result = Check("size(n.map(x, x)) + size(1.all(x, true))");

        Assert.Equal(
            [(CelErrorCode.InvalidComprehensionRange, 5), (CelErrorCode.NoMatchingOverload, 24), (CelErrorCode.InvalidComprehensionRange, 25)],
            result.Errors.Select(error => (error.Code, error.Location.Offset)));
    }

    // The type of the root against the expected type, after the traversal.
    [Theory]
    [InlineData("s", "int", "string", 0)]
    [InlineData("n + 1", "string", "int", 2)]
    [InlineData("[n]", "list(string)", "list(int)", 0)]
    [InlineData("{s: n}", "map(string, string)", "map(string, int)", 0)]
    [InlineData("nums[0]", "wrapper(string)", "int", 4)]
    [InlineData("null", "list(int)", "null_type", 0)]
    public static void TypeNotAssignableToTheExpectedType_IsExpectedTypeMismatchAtTheRoot(string expression, string expected, string actual, int offset)
    {
        CelError error = Assert.Single(Checker.Check(CheckingIndex.Parse(expression), Declared, Type(expected)).Errors);

        Assert.Equal((CelErrorCode.ExpectedTypeMismatch, CelErrorStage.Check, offset), (error.Code, error.Stage, error.Location.Offset));
        Assert.Equal("expression of type '" + actual + "' is not assignable to the expected type '" + expected + "'", error.Message);
    }

    // dyn on either side passes, a wrapper takes its primitive and null, a free type variable of the
    // expression is bound by the expected type, and a type parameter expects anything.
    [Theory]
    [InlineData("any", "int", "dyn")]
    [InlineData("n", "dyn", "int")]
    [InlineData("n", "wrapper(int)", "int")]
    [InlineData("null", "wrapper(int)", "null_type")]
    [InlineData("[]", "list(int)", "list(int)")]
    [InlineData("{}", "map(string, int)", "map(string, int)")]
    [InlineData("[[]]", "list(list(string))", "list(list(string))")]
    [InlineData("s", "T", "string")]
    [InlineData("[]", "list(_var0)", "list(dyn)")]
    public static void TypeAssignableToTheExpectedType_Passes(string expression, string expected, string resultType)
    {
        CheckResult result = Checker.Check(CheckingIndex.Parse(expression), Declared, Type(expected));

        Assert.True(result.IsSuccess);
        Assert.Equal(resultType, result.Expression.ResultType.ToString());
    }

    [Fact]
    public static void ExpressionWithAnError_IsNotCheckedAgainstTheExpectedType()
    {
        CelError error = Assert.Single(Checker.Check(CheckingIndex.Parse("missing"), Declared, CelType.Int).Errors);

        Assert.Equal(CelErrorCode.UndeclaredReference, error.Code);
    }

    // Checking stops at the node it cannot go into: the undeclared name inside is not reached.
    [Fact]
    public static void CheckingWithoutStack_IsInsufficientStackAtTheRoot()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();
        ParsedExpression parsed = CheckingIndex.Parse("\n  {'k': [missing]}");

        CheckResult result = CheckOnExhaustedStack(environment, parsed);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal((CelErrorCode.InsufficientStack, CelErrorStage.Check), (error.Code, error.Stage));
        Assert.Equal("insufficient execution stack to continue type checking", error.Message);
        Assert.Equal((3, 2, 3), (error.Location.Offset, error.Location.Line, error.Location.Column));
        Assert.Null(error.Exception);
    }

    // An exception inside the checker, here from a call whose argument array is not initialized, becomes
    // InternalError at that call, after the errors found before it, with the exception kept.
    [Fact]
    public static void ExceptionInsideTheChecker_IsInternalErrorAtTheNodeBeingChecked()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();
        var nodes = new NodeFactory();
        ExprNode key = nodes.Ident("missing", 1);
        ExprNode broken = nodes.Call("f", nodes.Ident("missing", 11), default, 13);
        StructEntry entry = nodes.Entry(key, broken, false, 8);
        ParsedExpression parsed = nodes.Build(nodes.Struct(string.Empty, [entry], 0), new LineTable("{missing:\n missing.f()}"));

        CheckResult result = environment.Check(parsed);

        Assert.Null(result.Expression);
        Assert.Equal(
            [(CelErrorCode.UndeclaredReference, 1), (CelErrorCode.UndeclaredReference, 11), (CelErrorCode.InternalError, 13)],
            result.Errors.Select(error => (error.Code, error.Location.Offset)));
        CelError error = result.Errors[2];
        Assert.Equal(CelErrorStage.Check, error.Stage);
        Assert.Equal("internal error during type checking; the original exception is in CelError.Exception", error.Message);
        Assert.Equal((2, 4), (error.Location.Line, error.Location.Column));
        Assert.NotNull(error.Exception);
    }

    [Fact]
    public static void ExceptionInsideTheChecker_LeavesTheEnvironmentUsable()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow();
        var nodes = new NodeFactory();
        ParsedExpression broken = nodes.Build(nodes.Call("f", null, default, 0), new LineTable("f()"));

        Assert.Equal(CelErrorCode.InternalError, Assert.Single(environment.Check(broken).Errors).Code);
        Assert.True(environment.Check(CheckingIndex.Parse("1 + 2")).IsSuccess);
    }

    private static CheckResult Check(string expression) => Checker.Check(CheckingIndex.Parse(expression), Declared);

    // The types of the expected-type cases, by their text.
    private static CelType Type(string text) => text switch
    {
        "int" => CelType.Int,
        "string" => CelType.String,
        "dyn" => CelType.Dyn,
        "T" => CelType.TypeParameter("T"),
        "list(int)" => CelType.ListOf(CelType.Int),
        "list(string)" => CelType.ListOf(CelType.String),
        "list(list(string))" => CelType.ListOf(CelType.ListOf(CelType.String)),
        "list(_var0)" => CelType.ListOf(CelType.TypeParameter("_var0")),
        "map(string, int)" => CelType.MapOf(CelType.String, CelType.Int),
        "map(string, string)" => CelType.MapOf(CelType.String, CelType.String),
        "wrapper(int)" => CelType.WrapperOf(CelType.Int),
        "wrapper(string)" => CelType.WrapperOf(CelType.String),
        _ => throw new ArgumentException("No type for " + text, nameof(text)),
    };

    // Descends until the runtime reports that the stack is nearly used up, then checks there.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static CheckResult CheckOnExhaustedStack(CelEnvironment environment, ParsedExpression parsed)
    {
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            return environment.Check(parsed);
        }

        Span<byte> frame = stackalloc byte[1024];
        frame.Fill(2);
        CheckResult result = CheckOnExhaustedStack(environment, parsed);
        Assert.Equal((byte)2, frame[^1]);
        return result;
    }
}

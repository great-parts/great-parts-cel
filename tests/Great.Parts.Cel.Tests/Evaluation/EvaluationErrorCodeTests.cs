using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Evaluation;

// The errors of the evaluation stage that the evaluator itself gives — 3011, 3014, 3024, 3028, 9000 and
// 9001 — with their messages and positions.
public sealed class EvaluationErrorCodeTests
{
    private static readonly IReadOnlyDictionary<string, CelValue> NoBindings = new Dictionary<string, CelValue>();

    [Theory]
    [InlineData("n", 0, "value of variable 'n' has type 'string', which does not match the declared type 'int'")]
    [InlineData("1 + n", 4, "value of variable 'n' has type 'string', which does not match the declared type 'int'")]
    public static void BindingOfAnotherType_IsBindingTypeMismatchAtTheIdentifier(string expression, int offset, string message)
    {
        CelError error = Plans.Error(Plans.Checked(expression).EvaluateOn(new Dictionary<string, CelValue> { ["n"] = CelValue.FromString("seven") }));

        Assert.Equal((CelErrorCode.BindingTypeMismatch, offset, message), (error.Code, error.Location.Offset, error.Message));
    }

    // A list bound to list(int) is checked at every element; the message names the type of the first
    // element that does not fit.
    [Fact]
    public static void ListWithAnElementOfAnotherKind_IsBindingTypeMismatch()
    {
        CelValue bound = CelValue.FromList(CelList.Create(CelValue.FromInt64(4), CelValue.FromBoolean(true), CelValue.FromString("x")));

        CelError error = Plans.Error(MacroPlans.Checked("ys").EvaluateOn(new Dictionary<string, CelValue> { ["ys"] = bound }));

        Assert.Equal(
            (CelErrorCode.BindingTypeMismatch, "value of variable 'ys' has type 'list(bool)', which does not match the declared type 'list(int)'"),
            (error.Code, error.Message));
    }

    // An error value fits no type, dyn included, and is named error.
    [Fact]
    public static void ErrorValueBoundToDyn_IsBindingTypeMismatchNamingError()
    {
        CelValue bound = Plans.Checked("7 / 0").EvaluateOn(NoBindings);

        CelError error = Plans.Error(Plans.Checked("any").EvaluateOn(new Dictionary<string, CelValue> { ["any"] = bound }));

        Assert.Equal("value of variable 'any' has type 'error', which does not match the declared type 'dyn'", error.Message);
    }

    // A later read gives the kept mismatch of the first read, at its identifier, without a second lookup.
    [Fact]
    public static void Mismatch_IsKeptForTheLaterReads()
    {
        var bindings = new CountingBindings(new() { ["flag"] = CelValue.FromString("yes") });

        CelError error = Plans.Error(Plans.Checked("[flag || true, flag]").EvaluateOn(bindings));

        Assert.Equal((CelErrorCode.BindingTypeMismatch, 1, 1), (error.Code, error.Location.Offset, bindings.Lookups));
    }

    // An enumeration on ulong above the range of int is ConversionRange at the read, not a mismatch.
    [Fact]
    public static void BoundEnumerationAboveInt_IsConversionRangeAtTheRead()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1).AddVariable("level", CelType.Int).Build().GetEnvironmentOrThrow();
        CelProgram<CelValue> program = environment.Compile<CelValue>("2 + level").GetProgramOrThrow();

        CelError error = Assert.Single(program.Evaluate(new CelBindings().Add("level", Wide.Top), cancellationToken: CancellationToken.None).Errors);

        Assert.Equal((CelErrorCode.ConversionRange, 4), (error.Code, error.Location.Offset));
    }

    [Fact]
    public static void ExceptionOfTheImplementation_IsApplicationExceptionWithTheException()
    {
        var thrown = new InvalidOperationException("broken");
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddFunction("explode", (Func<long, long>)(_ => throw thrown))
            .Build()
            .GetEnvironmentOrThrow();

        CelError error = Evaluate(environment, "2 + explode(1)");

        Assert.Equal(
            (CelErrorCode.ApplicationException, 11, "application code threw System.InvalidOperationException in overload 'explode_int' of function 'explode'", thrown),
            (error.Code, error.Location.Offset, error.Message, error.Exception));
    }

    [Fact]
    public static void ExceptionOfTheCostFunction_IsApplicationExceptionNamingIt()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddFunction(new CelFunctionDecl("weigh", CelOverloadDecl.Global("weigh_int", [CelType.Int], CelType.Int)
                .WithCost(_ => throw new ArgumentException("no cost"))))
            .AddFunctionImplementation("weigh_int", (long value) => value)
            .Build()
            .GetEnvironmentOrThrow();
        Plan plan = environment.Compile<CelValue>("weigh(3)").GetProgramOrThrow().Plan;

        CelError error = Plans.Error(plan.Evaluate(NoBindings, 100, CancellationToken.None));

        Assert.Equal(
            (CelErrorCode.ApplicationException, "application code threw System.ArgumentException in the cost function of overload 'weigh_int' of function 'weigh'"),
            (error.Code, error.Message));
    }

    // A value that the parameter of the typed delegate cannot take.
    [Fact]
    public static void ArgumentOutsideTheParameterType_IsArgumentConversion()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddFunction("narrow", (int value) => (long)value)
            .AddVariable("n", CelType.Int)
            .Build()
            .GetEnvironmentOrThrow();
        Plan plan = environment.Compile<CelValue>("narrow(n)").GetProgramOrThrow().Plan;

        CelError error = Plans.Error(plan.EvaluateOn(new Dictionary<string, CelValue> { ["n"] = CelValue.FromInt64(1L << 40) }));

        Assert.Equal(
            (CelErrorCode.ArgumentConversion, 6, "argument '1' of overload 'narrow_int' of function 'narrow' cannot be converted from 'int' to .NET type 'System.Int32'"),
            (error.Code, error.Location.Offset, error.Message));
    }

    [Theory]
    [InlineData("kind", "string")]
    [InlineData("deep", "list(string)")]
    public static void ResultOfAnotherType_IsImplementationResultMismatch(string function, string actual)
    {
        ArgumentNullException.ThrowIfNull(function);
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddFunction(new CelFunctionDecl("kind", CelOverloadDecl.Global("kind_int", [], CelType.Int)))
            .AddFunctionImplementation("kind_int", _ => CelValue.FromString("z"))
            .AddFunction(new CelFunctionDecl("deep", CelOverloadDecl.Global("deep_list", [], CelType.ListOf(CelType.Int))))
            .AddFunctionImplementation("deep_list", _ => CelValue.FromList(CelList.Create(CelValue.FromInt64(1), CelValue.FromString("z"))))
            .Build()
            .GetEnvironmentOrThrow();
        string expected = function == "kind" ? "int" : "list(int)";

        CelError error = Evaluate(environment, function + "()");

        Assert.Equal(
            (CelErrorCode.ImplementationResultMismatch, $"implementation of overload '{function}_{(function == "kind" ? "int" : "list")}' of function '{function}' returned a value of type '{actual}', expected '{expected}'"),
            (error.Code, error.Message));
    }

    // The input rows turn a null string into the null value, which does not fit string.
    [Fact]
    public static void NullStringOfATypedDelegate_IsImplementationResultMismatch()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddFunction("nothing", () => (string)null!)
            .Build()
            .GetEnvironmentOrThrow();

        CelError error = Evaluate(environment, "nothing()");

        Assert.Equal(
            "implementation of overload 'nothing' of function 'nothing' returned a value of type 'null_type', expected 'string'",
            error.Message);
    }

    // A null CelList of a typed delegate is the null value, which does not fit list(dyn).
    [Fact]
    public static void NullListOfATypedDelegate_IsImplementationResultMismatch()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddFunction("none", () => (CelList)null!)
            .Build()
            .GetEnvironmentOrThrow();

        CelError error = Evaluate(environment, "none()");

        Assert.Equal(
            "implementation of overload 'none' of function 'none' returned a value of type 'null_type', expected 'list(dyn)'",
            error.Message);
    }

    // A null CelList bound to a variable of type list(dyn) is the null value, which does not fit it.
    [Fact]
    public static void NullListBinding_IsBindingTypeMismatch()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddVariable("items", CelType.ListOf(CelType.Dyn))
            .Build()
            .GetEnvironmentOrThrow();
        CelProgram<CelValue> program = environment.Compile<CelValue>("items").GetProgramOrThrow();

        CelError error = Assert.Single(program.Evaluate(new CelBindings().Add("items", (CelList)null!), cancellationToken: CancellationToken.None).Errors);

        Assert.Equal(
            (CelErrorCode.BindingTypeMismatch, "value of variable 'items' has type 'null_type', which does not match the declared type 'list(dyn)'"),
            (error.Code, error.Message));
    }

    // A type parameter of the result is checked as dyn.
    [Fact]
    public static void ResultOfATypeParameter_IsNotChecked()
    {
        CelType a = CelType.TypeParameter("A");
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddFunction(new CelFunctionDecl("pick", CelOverloadDecl.Global("pick_list", [CelType.ListOf(a)], a)))
            .AddFunctionImplementation("pick_list", _ => CelValue.FromString("anything"))
            .Build()
            .GetEnvironmentOrThrow();

        Assert.Equal(
            CelValue.FromString("anything"),
            environment.Compile<CelValue>("pick([1])").GetProgramOrThrow().Plan.EvaluateOn(NoBindings));
    }

    // The node whose children the stack has no room for returns InsufficientStack.
    [Fact]
    public static void StackWithoutRoom_IsInsufficientStackAtTheNode()
    {
        Plan plan = Plans.Checked("1 + 2");

        CelError error = Plans.Error(WithoutStack(() => plan.EvaluateOn(NoBindings)));

        Assert.Equal(
            (CelErrorCode.InsufficientStack, CelErrorStage.Evaluate, 2, "insufficient execution stack to continue evaluation"),
            (error.Code, error.Stage, error.Location.Offset, error.Message));
    }

    // A lookup that throws is an exception of the library: InternalError at the variable.
    [Fact]
    public static void UnexpectedException_IsInternalErrorAtTheRecordedNode()
    {
        CelError error = Plans.Error(Plans.Checked("1 + n").EvaluateOn(new ThrowingBindings()));

        Assert.Equal(
            (CelErrorCode.InternalError, 4, "internal error during evaluation; the original exception is in CelError.Exception", typeof(KeyNotFoundException)),
            (error.Code, error.Location.Offset, error.Message, error.Exception?.GetType()));
    }

    private static CelError Evaluate(CelEnvironment environment, string expression) =>
        Plans.Error(environment.Compile<CelValue>(expression).GetProgramOrThrow().Plan.EvaluateOn(NoBindings));

    // Runs the step deeper and deeper on a thread of its own until the stack has no room left.
    private static CelValue WithoutStack(Func<CelValue> step)
    {
        CelValue result = default;
        var thread = new Thread(() => result = Deepen(step), 256 * 1024);
        thread.Start();
        thread.Join();
        return result;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static CelValue Deepen(Func<CelValue> step)
    {
        return RuntimeHelpers.TryEnsureSufficientExecutionStack() ? Deepen(step) : step();
    }

    private enum Wide : ulong
    {
        Top = ulong.MaxValue,
    }

    private sealed class ThrowingBindings : IReadOnlyDictionary<string, CelValue>
    {
        public int Count => 0;

        public IEnumerable<string> Keys => [];

        public IEnumerable<CelValue> Values => [];

        public CelValue this[string key] => throw new KeyNotFoundException(key);

        public bool ContainsKey(string key) => throw new KeyNotFoundException(key);

        public bool TryGetValue(string key, [MaybeNullWhen(false)] out CelValue value) => throw new KeyNotFoundException(key);

        public IEnumerator<KeyValuePair<string, CelValue>> GetEnumerator() => throw new NotSupportedException();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

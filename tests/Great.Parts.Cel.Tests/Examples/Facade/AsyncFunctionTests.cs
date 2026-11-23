using System;
using System.Threading;
using System.Threading.Tasks;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Examples.Evaluation;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Facade;

// Asynchronous functions of the application: the declaration forms and the overload they declare, the errors of the builder, the
// errors of a call — an exception, a result of another type — and what a synchronous evaluation of a program that calls one gives.
public sealed class AsyncFunctionTests
{
    private readonly AsyncFunctions functions = new();

    private static CelEnvironmentBuilder Builder() => new CelEnvironmentBuilder(CelStandardLibrary.V1).AddVariable<long>("x");

    [Fact]
    [Trait("Requirement", "F-23")]
    public void AddAsyncFunction_DeclaresTheOverloadOfTheDelegateWithoutTheToken()
    {
        CelEnvironment environment = Builder()
            .AddAsyncFunction("inc", (long value, CancellationToken token) => ValueTask.FromResult(value + 1))
            .Build()
            .GetEnvironmentOrThrow();

        CelOverloadDecl overload = Assert.Single(Assert.Single(environment.Functions, function => function.Name == "inc").Overloads);
        Assert.Equal("inc_int", overload.OverloadId);
        Assert.Equal(CelType.Int, Assert.Single(overload.ParameterTypes));
        Assert.Equal(CelType.Int, overload.ResultType);
        Assert.False(overload.IsReceiverStyle);
    }

    [Fact]
    [Trait("Requirement", "F-23")]
    public async Task AddAsyncMethod_DeclaresAnOverloadCalledOnAReceiver()
    {
        CelEnvironment environment = Builder()
            .AddAsyncMethod("upper", (string text, CancellationToken token) => ValueTask.FromResult(text.ToUpperInvariant()))
            .Build()
            .GetEnvironmentOrThrow();

        CelOverloadDecl overload = Assert.Single(Assert.Single(environment.Functions, function => function.Name == "upper").Overloads);
        Assert.Equal("string_upper", overload.OverloadId);
        Assert.True(overload.IsReceiverStyle);
        Assert.Equal("ABC", await EvaluateAsync<string>(environment, "'abc'.upper()").ConfigureAwait(true));
    }

    [Fact]
    [Trait("Requirement", "F-23")]
    public async Task EveryArityOfTheForms_IsCalled()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddAsyncFunction("zero", (CancellationToken token) => ValueTask.FromResult(0L))
            .AddAsyncFunction("one", (long a, CancellationToken token) => ValueTask.FromResult(a))
            .AddAsyncFunction("two", (long a, long b, CancellationToken token) => ValueTask.FromResult(a + b))
            .AddAsyncFunction("three", (long a, long b, long c, CancellationToken token) => ValueTask.FromResult(a + b + c))
            .AddAsyncFunction("four", (long a, long b, long c, long d, CancellationToken token) => ValueTask.FromResult(a + b + c + d))
            .AddAsyncMethod("m0", (long a, CancellationToken token) => ValueTask.FromResult(a))
            .AddAsyncMethod("m1", (long a, long b, CancellationToken token) => ValueTask.FromResult(a + b))
            .AddAsyncMethod("m2", (long a, long b, long c, CancellationToken token) => ValueTask.FromResult(a + b + c))
            .AddAsyncMethod("m3", (long a, long b, long c, long d, CancellationToken token) => ValueTask.FromResult(a + b + c + d))
            .Build()
            .GetEnvironmentOrThrow();

        string[] texts = ["zero()", "one(1)", "two(1, 2)", "three(1, 2, 3)", "four(1, 2, 3, 4)", "1.m0()", "1.m1(2)", "1.m2(2, 3)", "1.m3(2, 3, 4)"];
        long[] expected = [0L, 1L, 3L, 6L, 10L, 1L, 3L, 6L, 10L];
        for (int i = 0; i < texts.Length; i++)
        {
            Assert.Equal(expected[i], await EvaluateAsync<long>(environment, texts[i]).ConfigureAwait(true));
        }
    }

    [Fact]
    [Trait("Requirement", "F-23")]
    public async Task OptionsOfTheForms_GiveTheIdentifierAndTheCost()
    {
        var options = new CelFunctionOptions
        {
            OverloadId = "inc_v1",
            Cost = arguments => 7,
            Description = "adds one",
        };
        CelEnvironment environment = Builder()
            .AddAsyncFunction("inc", (long value, CancellationToken token) => ValueTask.FromResult(value + 1), options)
            .Build()
            .GetEnvironmentOrThrow();

        CelOverloadDecl overload = Assert.Single(Assert.Single(environment.Functions, function => function.Name == "inc").Overloads);
        Assert.Equal("inc_v1", overload.OverloadId);
        Assert.Equal("adds one", overload.Description);
        CelEvalResult<long> result = await environment.Compile<long>("inc(1)").GetProgramOrThrow()
            .EvaluateAsync(new CelBindings(), new CelEvalOptions { CostLimit = 1000 }, CancellationToken.None).ConfigureAwait(true);

        // The cost function gives 7 in each of the two passes; the cost of the rest of the call is the same in both.
        Assert.True(result.IsSuccess);
        CelEvalResult<long> cheaper = await Builder()
            .AddAsyncFunction("inc", (long value, CancellationToken token) => ValueTask.FromResult(value + 1))
            .Build()
            .GetEnvironmentOrThrow()
            .Compile<long>("inc(1)").GetProgramOrThrow()
            .EvaluateAsync(new CelBindings(), new CelEvalOptions { CostLimit = 1000 }, CancellationToken.None).ConfigureAwait(true);
        Assert.Equal(result.Cost - 14, cheaper.Cost);
    }

    [Fact]
    [Trait("Requirement", "F-23")]
    public async Task AddAsyncFunctionImplementation_ImplementsADeclaredOverload()
    {
        CelEnvironment environment = Builder()
            .AddFunction(new CelFunctionDecl("f", CelOverloadDecl.Global("f_int", [CelType.Int], CelType.Int)))
            .AddAsyncFunctionImplementation("f_int", (arguments, token) =>
            {
                arguments[0].TryGetInt64(out long value);
                return ValueTask.FromResult(CelValue.FromInt64(value * 10));
            })
            .Build()
            .GetEnvironmentOrThrow();

        Assert.Equal(30L, await EvaluateAsync<long>(environment, "f(3)").ConfigureAwait(true));
    }

    [Fact]
    [Trait("Requirement", "F-23")]
    public void SynchronousAndAsynchronousImplementationOfOneOverload_AreADuplicate()
    {
        CelEnvironmentBuilder builder = Builder()
            .AddFunction(new CelFunctionDecl("f", CelOverloadDecl.Global("f_int", [CelType.Int], CelType.Int)))
            .AddFunctionImplementation("f_int", arguments => arguments[0])
            .AddAsyncFunctionImplementation("f_int", (arguments, token) => ValueTask.FromResult(arguments[0]));

        CelError error = Assert.Single(builder.Build().Errors);

        Assert.Equal(CelErrorCode.DuplicateImplementation, error.Code);
    }

    [Fact]
    [Trait("Requirement", "F-23")]
    public void TwoAsynchronousImplementationsOfOneOverload_AreADuplicate()
    {
        CelEnvironmentBuilder builder = Builder()
            .AddAsyncFunction("inc", (long value, CancellationToken token) => ValueTask.FromResult(value + 1))
            .AddAsyncFunctionImplementation("inc_int", (arguments, token) => ValueTask.FromResult(arguments[0]));

        Assert.Equal(CelErrorCode.DuplicateImplementation, Assert.Single(builder.Build().Errors).Code);
    }

    [Fact]
    [Trait("Requirement", "F-23")]
    public void ArgumentsOfTheForms_AreChecked()
    {
        CelEnvironmentBuilder builder = Builder();
        Func<long, CancellationToken, ValueTask<long>> inc = (value, token) => ValueTask.FromResult(value + 1);

        _ = Assert.Throws<ArgumentNullException>(() => builder.AddAsyncFunction(null!, inc));
        _ = Assert.Throws<ArgumentException>(() => builder.AddAsyncFunction(string.Empty, inc));
        _ = Assert.Throws<ArgumentNullException>(() => builder.AddAsyncFunction<long, long>("inc", null!));
        _ = Assert.Throws<ArgumentNullException>(() => builder.AddAsyncFunction("inc", inc, null!));
        _ = Assert.Throws<ArgumentException>(() => builder.AddAsyncFunction("inc", inc, new CelFunctionOptions { OverloadId = string.Empty }));
        _ = Assert.Throws<ArgumentNullException>(() => builder.AddAsyncMethod<string, string>(null!, (text, token) => ValueTask.FromResult(text)));
        _ = Assert.Throws<ArgumentException>(() => builder.AddAsyncFunctionImplementation(string.Empty, (arguments, token) => ValueTask.FromResult(arguments[0])));
        _ = Assert.Throws<ArgumentNullException>(() => builder.AddAsyncFunctionImplementation("f_int", null!));
    }

    [Fact]
    [Trait("Requirement", "F-23")]
    [Trait("Requirement", "+R-9")]
    public async Task ExceptionOfTheFunction_IsApplicationExceptionAtTheCall()
    {
        CelEvalResult<long> result = await Program(functions.Async, "fail(1) + 1").EvaluateAsync(AsyncFunctions.Bindings(), null, CancellationToken.None).ConfigureAwait(true);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.ApplicationException, error.Code);
        InvalidOperationException exception = Assert.IsType<InvalidOperationException>(error.Exception);
        Assert.Equal("boom 1", exception.Message);
        Assert.Equal(4, error.Location.Offset);
    }

    [Fact]
    [Trait("Requirement", "F-23")]
    public async Task ResultOfAnotherType_IsImplementationResultMismatch()
    {
        CelEnvironment environment = Builder()
            .AddFunction(new CelFunctionDecl("f", CelOverloadDecl.Global("f_int", [CelType.Int], CelType.Int)))
            .AddAsyncFunctionImplementation("f_int", (arguments, token) => ValueTask.FromResult(CelValue.FromString("x")))
            .Build()
            .GetEnvironmentOrThrow();

        CelEvalResult<long> result = await Program(environment, "f(1)").EvaluateAsync(new CelBindings(), null, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(CelErrorCode.ImplementationResultMismatch, Assert.Single(result.Errors).Code);
    }

    [Fact]
    [Trait("Requirement", "F-23")]
    public async Task ArgumentThatTheDelegateCannotTake_IsArgumentConversion()
    {
        CelEnvironment environment = Builder()
            .AddAsyncFunction("narrow", (int value, CancellationToken token) => ValueTask.FromResult(value))
            .Build()
            .GetEnvironmentOrThrow();

        CelEvalResult<long> result = await Program(environment, "narrow(4294967296)").EvaluateAsync(new CelBindings(), null, CancellationToken.None).ConfigureAwait(true);

        Assert.Equal(CelErrorCode.ArgumentConversion, Assert.Single(result.Errors).Code);
    }

    [Theory]
    [Trait("Requirement", "F-23")]
    [InlineData("inc(1)", 3)]
    [InlineData("inc(1) + 1", 3)]
    [InlineData("[1].map(i, inc(i))", 14)]
    public void SynchronousEvaluationOfAProgramThatCallsAsyncFunction_IsAsyncEvaluationRequired(string expression, int offset)
    {
        CelProgram<CelValue> program = functions.Async.Compile<CelValue>(expression).GetProgramOrThrow();

        CelEvalResult<CelValue> result = program.Evaluate(AsyncFunctions.Bindings(), new CelEvalOptions { CostLimit = 1000 }, CancellationToken.None);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.AsyncEvaluationRequired, error.Code);
        Assert.Equal("inc", error.Subject);
        Assert.Equal(offset, error.Location.Offset);
        Assert.Equal(0UL, result.Cost);
        Assert.Equal(0, functions.Started);
    }

    [Fact]
    [Trait("Requirement", "F-23")]
    public void TheCheckIsMadeBeforeTheEvaluation_SoAShortCircuitDoesNotAvoidIt()
    {
        CelProgram<bool> program = functions.Async.Compile<bool>("false && inc(1) > 0").GetProgramOrThrow();

        CelEvalResult<bool> result = program.Evaluate(AsyncFunctions.Bindings(), cancellationToken: CancellationToken.None);

        Assert.Equal(CelErrorCode.AsyncEvaluationRequired, Assert.Single(result.Errors).Code);
        Assert.Null(result.Cost);
    }

    [Fact]
    [Trait("Requirement", "F-23")]
    public void FirstCallInTheText_IsThePlaceOfTheError()
    {
        CelProgram<long> program = functions.Async.Compile<long>("name('a').size() + inc(1)").GetProgramOrThrow();

        CelError error = Assert.Single(program.Evaluate(AsyncFunctions.Bindings(), cancellationToken: CancellationToken.None).Errors);

        Assert.Equal("name", error.Subject);
        Assert.Equal(4, error.Location.Offset);
    }

    [Fact]
    [Trait("Requirement", "F-23")]
    public void HasAsyncCalls_TellsWhetherTheProgramCallsOne()
    {
        Assert.True(functions.Async.Compile<long>("inc(1)").GetProgramOrThrow().HasAsyncCalls);
        Assert.False(functions.Async.Compile<long>("x + 1").GetProgramOrThrow().HasAsyncCalls);
        Assert.False(functions.Sync.Compile<long>("inc(1)").GetProgramOrThrow().HasAsyncCalls);
    }

    [Fact]
    [Trait("Requirement", "F-23")]
    public void OneStepSynchronousCall_IsAsyncEvaluationRequired()
    {
        CelEvalResult<long> result = functions.Async.Evaluate<long>("inc(1)", AsyncFunctions.Bindings(), cancellationToken: CancellationToken.None);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(CelErrorCode.AsyncEvaluationRequired, error.Code);
        Assert.Equal(CelErrorStage.Evaluate, error.Stage);
    }

    [Fact]
    [Trait("Requirement", "F-23")]
    public void SynchronousTwins_GiveTheSameValues()
    {
        Assert.Equal(2L, functions.Sync.Evaluate<long>("inc(1)", AsyncFunctions.Bindings(), cancellationToken: CancellationToken.None).GetValueOrThrow());
        Assert.Equal(3L, functions.Sync.Evaluate<long>("inc(inc(1))", AsyncFunctions.Bindings(), cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    private static CelProgram<long> Program(CelEnvironment environment, string expression) =>
        environment.Compile<long>(expression).GetProgramOrThrow();

    private static async Task<T> EvaluateAsync<T>(CelEnvironment environment, string expression)
    {
        CelEvalResult<T> result = await environment.Compile<T>(expression).GetProgramOrThrow()
            .EvaluateAsync(AsyncFunctions.Bindings(), null, CancellationToken.None).ConfigureAwait(true);
        return result.GetValueOrThrow();
    }
}

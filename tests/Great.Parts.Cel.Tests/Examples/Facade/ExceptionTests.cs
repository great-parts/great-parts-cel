using System;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Facade;

// The closed list of exceptions: a failed result read through its value, environment or program throws CelException
// with all errors, and its InnerException is the exception of the first error that has one; a result that was not returned by
// a call (default) is not successful, has no errors and throws InvalidOperationException when read.
[Trait("Requirement", "+R-20")]
public sealed class ExceptionTests
{
    [Fact]
    public void FailedBuild_EnvironmentAndGetOrThrow_ThrowCelException()
    {
        CelBuildResult result = new CelEnvironmentBuilder().AddVariable<long>("x").AddVariable<string>("x").Build();

        CelException fromProperty = Assert.Throws<CelException>(() => result.Environment);
        CelException fromMethod = Assert.Throws<CelException>(() => result.GetEnvironmentOrThrow());
        Assert.Equal(result.Errors, fromProperty.Errors);
        Assert.Equal(result.Errors, fromMethod.Errors);
        Assert.Null(fromProperty.InnerException);
    }

    [Fact]
    public void FailedCompilation_ProgramThrowsCelExceptionWithAllErrors()
    {
        CelEnvironment environment = new CelEnvironmentBuilder().Build().GetEnvironmentOrThrow();
        CelCompileResult<long> result = environment.Compile<long>("a + b");

        CelException exception = Assert.Throws<CelException>(() => result.Program);
        Assert.Equal(2, exception.Errors.Length);
        Assert.Equal(result.Errors, exception.Errors);
    }

    [Fact]
    public void FailedEvaluation_ValueThrowsCelException()
    {
        CelEvalResult<long> result = Compile("1 / 0").Evaluate(new CelBindings(), cancellationToken: CancellationToken.None);

        CelException exception = Assert.Throws<CelException>(() => result.Value);
        Assert.Equal(CelErrorCode.DivisionByZero, Assert.Single(exception.Errors).Code);
        Assert.Null(exception.InnerException);
    }

    // An exception of a function of the application becomes the error ApplicationException; the CelException of the failed
    // result carries it as InnerException as well as in CelError.Exception.
    [Fact]
    public void ApplicationException_IsTheInnerException()
    {
        var thrown = new InvalidOperationException("no stock");
        CelEnvironment environment = new CelEnvironmentBuilder()
            .AddFunction<long, long>("stock", _ => throw thrown)
            .Build()
            .GetEnvironmentOrThrow();
        CelEvalResult<long> result = environment.Compile<long>("stock(1)").GetProgramOrThrow()
            .Evaluate(new CelBindings(), cancellationToken: CancellationToken.None);

        CelException exception = Assert.Throws<CelException>(() => result.GetValueOrThrow());
        Assert.Same(thrown, exception.InnerException);
        Assert.Same(thrown, Assert.Single(exception.Errors).Exception);
    }

    [Fact]
    public void DefaultResult_IsNotSuccessfulAndHasNoErrors_AndItsValueAndCostAreInvalid()
    {
        CelEvalResult<long> result = default;

        Assert.False(result.IsSuccess);
        Assert.True(result.Errors.IsEmpty);
        Assert.False(result.Errors.IsDefault);
        Assert.Throws<InvalidOperationException>(() => result.Value);
        Assert.Throws<InvalidOperationException>(() => result.GetValueOrThrow());
        Assert.Throws<InvalidOperationException>(() => result.Cost);
    }

    // The default implementation of CreateInstance: the library never calls it for a type that is not constructible, so a call is a
    // violation of the state of the description.
    [Fact]
    public void CreateInstance_ByDefault_IsInvalidOperationException()
    {
        var type = new ReadOnlyType();

        Assert.False(type.Constructible);
        Assert.Throws<InvalidOperationException>(() => type.Create());
    }

    [Fact]
    public void Cancellation_IsOperationCanceledException()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => Compile("[1, 2, 3].map(i, i).size()").Evaluate(new CelBindings(), cancellationToken: source.Token));
    }

    private static CelProgram<long> Compile(string text) =>
        new CelEnvironmentBuilder().Build().GetEnvironmentOrThrow().Compile<long>(text).GetProgramOrThrow();

    // A described type without object literals: CreateInstance is not overridden.
    private sealed class ReadOnlyType : CelObjectType
    {
        public ReadOnlyType()
            : base("example.ReadOnly", typeof(ReadOnlyType), [])
        {
        }

        public bool Constructible => IsConstructible;

        public CelValue Create() => CreateInstance([], []);

        protected internal override CelValue GetField(object instance, int number) => throw new NotSupportedException();

        protected internal override bool HasField(object instance, int number) => false;
    }
}

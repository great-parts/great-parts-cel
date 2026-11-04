using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Evaluation;

// Cancellation of an evaluation. The events are the steps of macros and the calls of application
// functions; the evaluation checks the token before an event, so after the signal at most 64 events
// begin, and then OperationCanceledException of the token leaves the evaluation. The application
// function tick(int) counts its calls and signals the token on the call that the test names.
[Trait("Requirement", "+R-11")]
public sealed class CancellationTests
{
    private readonly Ticks ticks = new();
    private readonly CelEnvironment environment;

    public CancellationTests()
    {
        Ticks counter = ticks;
        environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddVariable("l", CelType.ListOf(CelType.Int))
            .AddFunction<long, long>("tick", value =>
            {
                counter.Calls++;
                if (counter.Calls == counter.CancelAt)
                {
                    counter.Cancel?.Invoke();
                }

                return value;
            })
            .Build()
            .GetEnvironmentOrThrow();
    }

    // 200 calls in one list literal, the signal during the first: the calls that begin after it are at
    // most 64, far fewer than the 199 that are left.
    [Fact]
    public void SignalDuringACall_StopsTheEvaluationWithin64Events()
    {
        string expression = "[" + string.Join(", ", Enumerable.Range(0, 200).Select(index => "tick(" + index.ToString(CultureInfo.InvariantCulture) + ")")) + "]";
        CelProgram<CelValue> program = environment.Compile<CelValue>(expression).GetProgramOrThrow();
        using var source = new CancellationTokenSource();
        ticks.Cancel = source.Cancel;
        ticks.CancelAt = 1;

        OperationCanceledException exception = Assert.ThrowsAny<OperationCanceledException>(
            () => program.Evaluate(new CelBindings(), cancellationToken: source.Token));

        Assert.Equal(source.Token, exception.CancellationToken);
        Assert.InRange(ticks.Calls - 1, 0, 64);
    }

    // Over a list of 1 000 elements each step is an event and so is the call in its predicate: the calls
    // after the signal, during the third call, are at most 64 as well.
    [Fact]
    public void SignalDuringAMacro_StopsTheEvaluationWithin64Events()
    {
        CelProgram<bool> program = environment.Compile<bool>("l.all(x, tick(x) >= 0)").GetProgramOrThrow();
        var bindings = new CelBindings().Add("l", CelList.Create(Enumerable.Range(0, 1000).Select(index => CelValue.FromInt64(index)).ToArray()));
        using var source = new CancellationTokenSource();
        ticks.Cancel = source.Cancel;
        ticks.CancelAt = 3;

        Assert.ThrowsAny<OperationCanceledException>(() => program.Evaluate(bindings, cancellationToken: source.Token));

        Assert.InRange(ticks.Calls - 3, 0, 64);
    }

    // The token is checked before the first event: a canceled token stops the evaluation before any
    // step or call begins.
    [Fact]
    public void CanceledToken_StopsTheEvaluationBeforeTheFirstEvent()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var bindings = new CelBindings().Add("l", CelList.Create(CelValue.FromInt64(1)));

        Assert.ThrowsAny<OperationCanceledException>(() => environment.Compile<CelValue>("tick(1)").GetProgramOrThrow()
            .Evaluate(new CelBindings(), cancellationToken: source.Token));
        Assert.ThrowsAny<OperationCanceledException>(() => environment.Compile<bool>("l.all(x, x > 0)").GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: source.Token));
        Assert.Equal(0, ticks.Calls);
    }

    // An application function that throws OperationCanceledException of the canceled token of the
    // evaluation cancels it: the exception is not an ApplicationException error.
    [Fact]
    public void ApplicationCodeThatThrowsForTheCanceledToken_CancelsTheEvaluation()
    {
        using var source = new CancellationTokenSource();
        CelEnvironment throwing = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddFunction<long, long>("stop", value =>
            {
                source.Cancel();
                source.Token.ThrowIfCancellationRequested();
                return value;
            })
            .Build()
            .GetEnvironmentOrThrow();

        OperationCanceledException exception = Assert.ThrowsAny<OperationCanceledException>(
            () => throwing.Compile<long>("stop(1)").GetProgramOrThrow().Evaluate(new CelBindings(), cancellationToken: source.Token));

        Assert.Equal(source.Token, exception.CancellationToken);
    }

    // Without a token that can be canceled the same evaluation runs to its end.
    [Fact]
    public void EvaluationWithoutAToken_RunsToTheEnd()
    {
        var bindings = new CelBindings().Add("l", CelList.Create(Enumerable.Range(0, 1000).Select(index => CelValue.FromInt64(index)).ToArray()));

        Assert.True(environment.Compile<bool>("l.all(x, tick(x) >= 0)").GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None).GetValueOrThrow());
        Assert.Equal(1000, ticks.Calls);
    }

    private sealed class Ticks
    {
        public int Calls { get; set; }

        public int CancelAt { get; set; }

        public Action? Cancel { get; set; }
    }
}

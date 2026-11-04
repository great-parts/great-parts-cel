using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Evaluation;

// The two errors of any stage, at evaluation. InsufficientStack: a node that would evaluate its children
// with the execution stack nearly used up returns the error instead. InternalError: an unexpected
// exception inside evaluation becomes the error with the original exception, at the node whose own
// operation ran. A correct library has no such exception on any input of the public API, so the example
// brings one in through the internal entry point of the plan: bindings whose lookup throws, at the read of
// the variable.
public sealed class StackAndInternalErrorTests
{
    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable<long>("x")
        .Build()
        .GetEnvironmentOrThrow();

    // The program is compiled on a normal stack and evaluated where the stack is nearly used up: the call
    // "+" at 2 does not evaluate its operands.
    [Fact]
    [Trait("Requirement", "+R-9")]
    [Trait("Requirement", "F-4")]
    public void EvaluationWithoutStack_IsInsufficientStackAtTheNodeThatDidNotDescend()
    {
        CelProgram<long> program = environment.Compile<long>("x + 1").GetProgramOrThrow();

        CelEvalResult<long> result = EvaluateOnExhaustedStack(program, new CelBindings().Add("x", 41L));

        CelError error = Assert.Single(result.Errors);
        Assert.Equal((CelErrorCode.InsufficientStack, CelErrorStage.Evaluate), (error.Code, error.Stage));
        Assert.Equal("insufficient execution stack to continue evaluation", error.Message);
        Assert.Equal((2, 1, 3), (error.Location.Offset, error.Location.Line, error.Location.Column));
    }

    [Fact]
    [Trait("Requirement", "+R-9")]
    [Trait("Requirement", "F-4")]
    public void UnexpectedExceptionInsideEvaluation_IsInternalErrorWithTheException()
    {
        CelProgram<long> program = environment.Compile<long>("1 + x").GetProgramOrThrow();
        var bindings = new ThrowingBindings();

        CelValue value = program.Plan.Evaluate(bindings, null, CancellationToken.None);

        Assert.True(value.TryGetError(out CelError? error));
        Assert.Equal((CelErrorCode.InternalError, CelErrorStage.Evaluate), (error.Code, error.Stage));
        Assert.Equal("internal error during evaluation; the original exception is in CelError.Exception", error.Message);
        Assert.Same(bindings.Thrown, error.Exception);
        Assert.Equal((4, 1, 5), (error.Location.Offset, error.Location.Line, error.Location.Column));
    }

    // Descends until the runtime reports that the stack is nearly used up, then evaluates there.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static CelEvalResult<long> EvaluateOnExhaustedStack(CelProgram<long> program, CelBindings bindings)
    {
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            return program.Evaluate(bindings, cancellationToken: CancellationToken.None);
        }

        Span<byte> frame = stackalloc byte[1024];
        frame.Fill(1);
        CelEvalResult<long> result = EvaluateOnExhaustedStack(program, bindings);
        Assert.Equal((byte)1, frame[^1]);
        return result;
    }

    // Bindings whose every lookup throws the same exception.
    private sealed class ThrowingBindings : IReadOnlyDictionary<string, CelValue>
    {
        public InvalidOperationException Thrown { get; } = new("lookup failed");

        public int Count => throw Thrown;

        public IEnumerable<string> Keys => throw Thrown;

        public IEnumerable<CelValue> Values => throw Thrown;

        public CelValue this[string key] => throw Thrown;

        public bool ContainsKey(string key) => throw Thrown;

        public bool TryGetValue(string key, [MaybeNullWhen(false)] out CelValue value) => throw Thrown;

        public IEnumerator<KeyValuePair<string, CelValue>> GetEnumerator() => throw Thrown;

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

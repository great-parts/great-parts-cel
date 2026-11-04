using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Evaluation;

// Cancellation: the events — calls of application functions and steps of comprehensions — are counted
// when the token can be canceled; the token is checked before the first event and then every 64, so
// after the signal at most 64 events begin. With CancellationToken.None nothing is counted.
public sealed class CancellationTests
{
    private static readonly IReadOnlyDictionary<string, CelValue> NoBindings = new Dictionary<string, CelValue>();

    // The signal comes inside the 10th call: the check before the 65th event throws, so 64 calls began.
    [Fact]
    public static void AfterTheSignal_AtMost64EventsBegin()
    {
        using var source = new CancellationTokenSource();
        int calls = 0;
        Plan plan = Calls(200, () =>
        {
            if (++calls == 10)
            {
                source.Cancel();
            }

            return 1L;
        });

        OperationCanceledException canceled = Assert.ThrowsAny<OperationCanceledException>(() => plan.Evaluate(NoBindings, null, source.Token));

        Assert.Equal((64, source.Token), (calls, canceled.CancellationToken));
    }

    // The token is checked before the first event.
    [Fact]
    public static void CanceledToken_StopsBeforeTheFirstEvent()
    {
        int calls = 0;
        Plan plan = Calls(3, () => ++calls);

        _ = Assert.ThrowsAny<OperationCanceledException>(() => plan.Evaluate(NoBindings, null, new CancellationToken(true)));

        Assert.Equal(0, calls);
    }

    // Without events the token is not checked.
    [Fact]
    public static void CanceledToken_WithoutEvents_GivesTheValue()
    {
        Assert.Equal(CelValue.FromInt64(3), Plans.Checked("1 + 2").Evaluate(NoBindings, null, new CancellationToken(true)));
    }

    // With CancellationToken.None every call runs, whatever the application does with other tokens.
    [Fact]
    public static void NoneToken_CountsNothing()
    {
        int calls = 0;
        Plan plan = Calls(200, () => ++calls);

        Assert.True(plan.Evaluate(NoBindings, null, CancellationToken.None).TryGetList(out CelList? list));
        Assert.Equal((200, 200), (list.Count, calls));
    }

    // The steps of a comprehension are events too.
    [Fact]
    public static void ComprehensionStep_IsAnEvent()
    {
        Plan plan = MacroPlans.Checked("xs.all(v, true)");
        var bindings = new Dictionary<string, CelValue> { ["xs"] = MacroPlans.Integers(3) };

        _ = Assert.ThrowsAny<OperationCanceledException>(() => plan.Evaluate(bindings, null, new CancellationToken(true)));
        using var source = new CancellationTokenSource();
        Assert.Equal(CelValue.FromBoolean(true), plan.Evaluate(bindings, null, source.Token));
    }

    // An OperationCanceledException of another token, thrown by application code, is that code's exception.
    [Fact]
    public static void CancellationOfAnotherToken_IsApplicationException()
    {
        using var source = new CancellationTokenSource();
        Plan plan = Calls(1, () => throw new OperationCanceledException(new CancellationToken(true)));

        CelError error = Plans.Error(plan.Evaluate(NoBindings, null, source.Token));

        Assert.Equal(CelErrorCode.ApplicationException, error.Code);
    }

    // A list of count calls of next(): [next(), next(), …].
    private static Plan Calls(int count, Func<long> next)
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1).AddFunction("next", next).Build().GetEnvironmentOrThrow();
        string expression = "[" + string.Join(", ", Enumerable.Repeat("next()", count)) + "]";
        return environment.Compile<CelValue>(expression).GetProgramOrThrow().Plan;
    }
}

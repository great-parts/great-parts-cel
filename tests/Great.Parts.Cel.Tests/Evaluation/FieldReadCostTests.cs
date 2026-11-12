using System;
using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Evaluation.MessageSamples;

namespace Great.Parts.Cel.Tests.Evaluation;

// The units that a description adds to the read of a field, FieldReadCost (docs/design/evaluation.md, "Charges of a described
// object"): charged after the unit of the selection and before the read, only with a cost limit, for a selection and not for has().
public sealed class FieldReadCostTests
{
    [Fact]
    public static void ReadOfAField_ChargesTheUnitsOfTheDescriptionAfterTheUnitOfTheSelection()
    {
        ulong none = Cost(NewMessageType(), "msg.id");
        ulong five = Cost(NewMessageType(readCost: static _ => 5), "msg.id");
        ulong twice = Cost(NewMessageType(readCost: static _ => 5), "msg.id + msg.id");
        ulong once = Cost(NewMessageType(readCost: static _ => 0), "msg.id + msg.id");

        Assert.Equal(5UL, five - none);
        Assert.Equal(10UL, twice - once);
    }

    [Fact]
    public static void UnitsAreAskedByTheNumberOfTheField()
    {
        Func<int, long> byField = static number => number == Label ? 40 : 0;

        Assert.Equal(40UL, Cost(NewMessageType(readCost: byField), "msg.label") - Cost(NewMessageType(), "msg.label"));
        Assert.Equal(0UL, Cost(NewMessageType(readCost: byField), "msg.id") - Cost(NewMessageType(), "msg.id"));
    }

    [Fact]
    public static void ReadByTheNameOfAValueOfTheTypeDyn_ChargesThemToo()
    {
        MessageType type = NewMessageType(readCost: static _ => 7);
        CelEnvironment environment = CreateEnvironment(type);
        Dictionary<string, CelValue> bindings = Bind(type);

        Plan plan = environment.Compile<CelValue>("d.id").GetProgramOrThrow().Plan;
        Frame frame = plan.CreateFrame(bindings, ulong.MaxValue, CancellationToken.None);
        _ = plan.Run(frame);
        ulong withUnits = frame.ChargedUnits;
        MessageType free = NewMessageType();
        Plan freePlan = CreateEnvironment(free).Compile<CelValue>("d.id").GetProgramOrThrow().Plan;
        Frame freeFrame = freePlan.CreateFrame(Bind(free), ulong.MaxValue, CancellationToken.None);
        _ = freePlan.Run(freeFrame);

        Assert.Equal(7UL, withUnits - freeFrame.ChargedUnits);
        Assert.Equal(1, type.ReadCostCalls);
    }

    [Fact]
    public static void WithoutACostLimit_TheDescriptionIsNeverAsked()
    {
        MessageType type = NewMessageType(readCost: static _ => 5);
        Plan plan = CreateEnvironment(type).Compile<CelValue>("msg.id + msg.id").GetProgramOrThrow().Plan;

        CelValue result = plan.Run(plan.CreateFrame(Bind(type), null, CancellationToken.None));

        Assert.Equal(CelValue.FromInt64(0), result);
        Assert.Equal(0, type.ReadCostCalls);
    }

    [Fact]
    public static void PresenceTest_NeverAsksTheDescription()
    {
        MessageType type = NewMessageType(readCost: static _ => 5);
        Plan plan = CreateEnvironment(type).Compile<CelValue>("has(msg.id)").GetProgramOrThrow().Plan;

        CelValue result = plan.Run(plan.CreateFrame(Bind(type), ulong.MaxValue, CancellationToken.None));

        Assert.Equal(CelValue.FromBoolean(true), result);
        Assert.Equal(0, type.ReadCostCalls);
    }

    [Fact]
    public static void UnitsAboveTheLimit_AreCostLimitExceededAtTheSelection()
    {
        MessageType type = NewMessageType(readCost: static _ => 5);
        const string Expression = "msg.id";
        ulong free = Cost(NewMessageType(), Expression);
        Plan plan = CreateEnvironment(type).Compile<CelValue>(Expression).GetProgramOrThrow().Plan;

        CelError error = ErrorOf(plan.Run(plan.CreateFrame(Bind(type), free + 4, CancellationToken.None)));

        Assert.Equal((CelErrorCode.CostLimitExceeded, Expression.IndexOf('.', StringComparison.Ordinal)), (error.Code, error.Location.Offset));
        Assert.Equal(CelValue.FromInt64(0), plan.Run(plan.CreateFrame(Bind(type), free + 5, CancellationToken.None)));
    }

    [Fact]
    public static void ExceptionOfTheDescription_IsApplicationExceptionOfTheFieldAtTheSelection()
    {
        var thrown = new InvalidOperationException("boom");
        MessageType type = NewMessageType(readCost: _ => throw thrown);
        const string Expression = "msg.id";
        Plan plan = CreateEnvironment(type).Compile<CelValue>(Expression).GetProgramOrThrow().Plan;

        CelError error = ErrorOf(plan.Run(plan.CreateFrame(Bind(type), ulong.MaxValue, CancellationToken.None)));

        Assert.Equal((CelErrorCode.ApplicationException, Expression.IndexOf('.', StringComparison.Ordinal)), (error.Code, error.Location.Offset));
        Assert.Equal("application code threw System.InvalidOperationException in field 'id' of type 'acme.test.Message'", error.Message);
        Assert.Same(thrown, error.Exception);
    }

    // The units that the evaluation charged, with a limit that is never reached.
    private static ulong Cost(MessageType type, string expression)
    {
        Plan plan = CreateEnvironment(type).Compile<CelValue>(expression).GetProgramOrThrow().Plan;
        Frame frame = plan.CreateFrame(Bind(type), ulong.MaxValue, CancellationToken.None);
        _ = plan.Run(frame);
        return frame.ChargedUnits;
    }

    // An instance of the message with its id set, as the variables msg and d.
    private static Dictionary<string, CelValue> Bind(MessageType type)
    {
        var message = new Message();
        message.Values[Id] = CelValue.FromInt64(0);
        message.Values[Label] = CelValue.FromString("x");
        CelValue value = CelValue.FromObject(type, message);
        return new Dictionary<string, CelValue> { ["msg"] = value, ["d"] = value };
    }
}

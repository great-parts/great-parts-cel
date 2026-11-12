using System;
using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Values.ObjectTypeTests;

namespace Great.Parts.Cel.Tests.Values;

// The equality that a description does itself (docs/design/objects.md, "Members for protobuf messages"): EqualityCost says that it
// does — null for the default rule, a number of units charged once for its own CelEquals — and the members of CelObjectType that
// the library calls with a default: CreateInstance, IsConstructible, FieldReadCost and EqualityCost.
public sealed class CustomEqualityTests
{
    [Fact]
    public static void DescriptionThatOverridesNothing_IsNotConstructible_AndHasNoExtraCosts()
    {
        CelObjectType description = new OrderType("acme.Order", [new CelObjectField(0, "total", CelType.Int)]);

        Assert.False(description.IsConstructible);
        Assert.Throws<NotSupportedException>(() => description.CreateInstance([], []));
        Assert.Equal(0, description.FieldReadCost(new object(), 0));
        Assert.Null(description.EqualityCost(new object(), new object()));
    }

    // The description decides: equal by its CelEquals although the fields differ, and the other way round.
    [Theory]
    [InlineData(true, 1L, 2L, true)]
    [InlineData(false, 3L, 3L, false)]
    public static void EqualityOfTwoInstances_IsTheResultOfCelEquals_WhenEqualityCostSaysSo(bool answer, long left, long right, bool expected)
    {
        BoxType type = new(static (_, _) => 5, (_, _) => CelValue.FromBoolean(answer));

        (CelValue equal, _) = Run(type, "a == b", left, right);
        (CelValue unequal, _) = Run(type, "a != b", left, right);

        Assert.Equal(CelValue.FromBoolean(expected), equal);
        Assert.Equal(CelValue.FromBoolean(!expected), unequal);
    }

    // The number is charged once, before the comparison, and replaces the charges of the default rule.
    [Fact]
    public static void EqualityCost_IsChargedOnce()
    {
        ulong seven = Run(new BoxType(static (_, _) => 7, null), "a == b", 1, 1).Cost;
        ulong two = Run(new BoxType(static (_, _) => 2, null), "a == b", 1, 1).Cost;
        ulong defaultRule = Run(new BoxType(null, null), "a == b", 1, 1).Cost;

        Assert.Equal(5UL, seven - two);
        Assert.NotEqual(two, defaultRule);
    }

    [Fact]
    public static void EqualityCostOfNull_IsTheDefaultRule_AndCelEqualsIsNotCalledByTheCore()
    {
        BoxType type = new(null, null);

        (CelValue same, _) = Run(type, "a == b", 4, 4);
        (CelValue different, _) = Run(type, "a == b", 4, 5);

        Assert.Equal((CelValue.FromBoolean(true), CelValue.FromBoolean(false)), (same, different));
        Assert.Equal(0, type.EqualsCalls);
    }

    // With no cost limit nothing is charged, and CelEquals decides all the same.
    [Fact]
    public static void WithoutACostLimit_TheNumberIsNotCharged_AndCelEqualsDecides()
    {
        BoxType type = new(static (_, _) => long.MaxValue, (_, _) => CelValue.FromBoolean(true));
        (CelEnvironment environment, Dictionary<string, CelValue> bindings) = Create(type, 1, 2);
        Plan plan = environment.Compile<CelValue>("a == b").GetProgramOrThrow().Plan;

        CelValue result = plan.Run(plan.CreateFrame(bindings, null, CancellationToken.None));

        Assert.Equal(CelValue.FromBoolean(true), result);
        Assert.Equal(1, type.EqualsCalls);
    }

    [Theory]
    [InlineData("[a] == [b]")]
    [InlineData("a in [b]")]
    [InlineData("[[a]] == [[b]]")]
    [InlineData("{'k': a} == {'k': b}")]
    public static void EqualityInsideACollection_AsksTheDescriptionOnce(string expression)
    {
        BoxType type = new(static (_, _) => 3, (_, _) => CelValue.FromBoolean(true));

        (CelValue result, _) = Run(type, expression, 1, 2);

        Assert.Equal(CelValue.FromBoolean(true), result);
        Assert.Equal(1, type.EqualsCalls);
    }

    [Fact]
    public static void ErrorValueOfCelEquals_IsTheResultAtTheOperator()
    {
        BoxType type = new(static (_, _) => 1, null);
        type.Decide = (_, _) => ObjectRules.FieldError(type, 0, CelErrorCode.InvalidArgument, "bad pair");
        const string Expression = "a == b";

        CelError error = ErrorOf(Run(type, Expression, 1, 1).Value);

        Assert.Equal((CelErrorCode.InvalidArgument, Expression.IndexOf('=', StringComparison.Ordinal)), (error.Code, error.Location.Offset));
        Assert.Contains("bad pair", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public static void ResultOfCelEqualsThatIsNeitherABoolNorAnError_IsInternalError()
    {
        BoxType type = new(static (_, _) => 1, (_, _) => CelValue.FromInt64(1));

        CelError error = ErrorOf(Run(type, "a == b", 1, 1).Value);

        Assert.Equal(CelErrorCode.InternalError, error.Code);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public static void ExceptionOfCelEqualsOrOfEqualityCost_IsApplicationExceptionAtTheOperator(bool inTheCost)
    {
        var thrown = new InvalidOperationException("boom");
        BoxType type = inTheCost
            ? new BoxType((_, _) => throw thrown, null)
            : new BoxType(static (_, _) => 1, (_, _) => throw thrown);
        const string Expression = "a == b";

        CelError error = ErrorOf(Run(type, Expression, 1, 1).Value);

        Assert.Equal((CelErrorCode.ApplicationException, Expression.IndexOf('=', StringComparison.Ordinal)), (error.Code, error.Location.Offset));
        Assert.Equal("application code threw System.InvalidOperationException in the equality of objects of type 'acme.test.Box'", error.Message);
        Assert.Same(thrown, error.Exception);
    }

    // The same description is asked for each pair, so a list of two pairs is two calls.
    [Fact]
    public static void EachPairOfInstances_IsComparedByItsOwnCall()
    {
        BoxType type = new(static (_, _) => 1, (_, _) => CelValue.FromBoolean(true));

        (CelValue result, _) = Run(type, "[a, b] == [b, a]", 1, 2);

        Assert.Equal(CelValue.FromBoolean(true), result);
        Assert.Equal(2, type.EqualsCalls);
    }

    private static (CelValue Value, ulong Cost) Run(BoxType type, string expression, long left, long right)
    {
        (CelEnvironment environment, Dictionary<string, CelValue> bindings) = Create(type, left, right);
        Plan plan = environment.Compile<CelValue>(expression).GetProgramOrThrow().Plan;
        Frame frame = plan.CreateFrame(bindings, ulong.MaxValue, CancellationToken.None);
        CelValue value = plan.Run(frame);
        return (value, frame.ChargedUnits);
    }

    private static (CelEnvironment Environment, Dictionary<string, CelValue> Bindings) Create(BoxType type, long left, long right)
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddTypes(new BoxProvider(type))
            .AddVariable("a", type.Type)
            .AddVariable("b", type.Type)
            .Build()
            .GetEnvironmentOrThrow();
        var bindings = new Dictionary<string, CelValue>
        {
            ["a"] = CelValue.FromObject(type, new Box { Value = left }),
            ["b"] = CelValue.FromObject(type, new Box { Value = right }),
        };
        return (environment, bindings);
    }

    private static CelError ErrorOf(CelValue value) =>
        value.TryGetError(out CelError? error) ? error : throw new InvalidOperationException("The value is not an error value: " + value.Kind);

    private sealed class BoxProvider(CelObjectType type) : CelTypeProvider("box", [type], []);

    internal sealed class Box
    {
        internal long Value { get; init; }
    }

    // A description that compares its instances itself when the cost function gives a number, by the function equals.
    internal sealed class BoxType(Func<object, object, long?>? cost, Func<object, object, CelValue>? equals)
        : CelObjectType("acme.test.Box", typeof(Box), [new CelObjectField(0, "value", CelType.Int)])
    {
        internal int EqualsCalls { get; private set; }

        internal Func<object, object, CelValue>? Decide { get; set; } = equals;

        protected internal override CelValue GetField(object instance, int number) => CelValue.FromInt64(((Box)instance).Value);

        protected internal override bool HasField(object instance, int number) => true;

        protected internal override long? EqualityCost(object left, object right) => cost?.Invoke(left, right);

        protected internal override CelValue CelEquals(object left, object right)
        {
            EqualsCalls++;
            return Decide is null ? base.CelEquals(left, right) : Decide(left, right);
        }
    }
}

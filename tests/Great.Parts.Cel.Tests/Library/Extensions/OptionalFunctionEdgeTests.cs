using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Library.Extensions;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Library.Extensions;

// The functions of the set optional at their edges, which the examples do not name: the zero values of JSON data and of objects of the
// application with the units of their fields, an element of a list of the application that is an error value, an element that is no
// optional, and an argument of a kind that no overload takes, called through the implementation as the planner calls it.
public sealed class OptionalFunctionEdgeTests
{
    private static readonly LineTable Lines = new("optional.ofNonZeroValue(x)");

    private static readonly CelEnvironment Environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Optional(2))
        .AddVariable("j", CelType.Dyn)
        .AddVariable<IReadOnlyList<long>>("broken")
        .Build()
        .GetEnvironmentOrThrow();

    [Theory]
    [InlineData("0", false)]
    [InlineData("0.0", false)]
    [InlineData("\"\"", false)]
    [InlineData("[]", false)]
    [InlineData("{}", false)]
    [InlineData("null", false)]
    [InlineData("false", false)]
    [InlineData("1", true)]
    [InlineData("0.5", true)]
    [InlineData("\"a\"", true)]
    [InlineData("[0]", true)]
    [InlineData("{\"a\": 0}", true)]
    [InlineData("true", true)]
    public static void OfNonZeroValue_OfAJsonElement_IsEmptyForTheZeroOfItsKind(string json, bool hasValue)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        CelBindings bindings = new CelBindings().Add("j", document.RootElement);

        CelEvalResult<bool> result = Environment.Compile<bool>("optional.ofNonZeroValue(j).hasValue()").GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None);

        Assert.Equal(hasValue, result.GetValueOrThrow());
    }

    [Fact]
    public static void OfNonZeroValue_OfAnObjectWithNoFieldSet_IsEmpty_AndOneWithAFieldSetHoldsIt()
    {
        var type = new ProbeType();
        var site = new CallSite("optional.ofNonZeroValue", 0, Lines);

        CelValue none = OptionalFunctions.OfNonZeroValue([CelValue.FromObject(type, new Probe())], in site);
        CelValue second = OptionalFunctions.OfNonZeroValue([CelValue.FromObject(type, new Probe { Second = true })], in site);

        Assert.True(none.TryGetOptional(out CelOptional<CelValue> emptied));
        Assert.False(emptied.HasValue);
        Assert.True(second.TryGetOptional(out CelOptional<CelValue> held));
        Assert.True(held.HasValue);
        Assert.Equal(CelValueKind.Object, held.Value.Kind);
    }

    [Fact]
    public static void OfNonZeroValue_OfAnObject_ChargesTheUnitsOfItsFieldsOnce_AndOfAnyOtherValueNothing()
    {
        var type = new ProbeType();
        var meter = new RecordingMeter();
        var site = new CallSite("optional.ofNonZeroValue", 0, Lines, meter);

        _ = OptionalFunctions.OfNonZeroValue([CelValue.FromObject(type, new Probe())], in site);
        _ = OptionalFunctions.OfNonZeroValue([CelValue.FromObject(type, new Probe { First = true })], in site);
        _ = OptionalFunctions.OfNonZeroValue([CelValue.FromInt64(1)], in site);
        _ = OptionalFunctions.OfNonZeroValue([CelValue.FromString(string.Empty)], in site);

        Assert.Equal(new ulong[] { 2, 2 }, meter.Charges);
    }

    [Fact]
    public static void OfNonZeroValue_OfAnObjectWhoseTypeNoSourceDescribes_IsTypeNotAccessible()
    {
        var site = new CallSite("optional.ofNonZeroValue", 0, Lines);

        CelValue result = OptionalFunctions.OfNonZeroValue([ProbeType.Bare(new Probe())], in site);

        Assert.True(result.TryGetError(out CelError? error));
        Assert.Equal(CelErrorCode.TypeNotAccessible, error.Code);
    }

    [Theory]
    [InlineData("broken.first()")]
    [InlineData("broken.last()")]
    public static void FirstAndLast_OfAListOfTheApplicationWhoseElementFails_AreThatError(string expression)
    {
        var list = new BrokenList();
        CelBindings bindings = new CelBindings().Add<IReadOnlyList<long>>("broken", list);

        CelEvalResult<CelValue> result = Environment.Compile<CelValue>(expression).GetProgramOrThrow()
            .Evaluate(bindings, cancellationToken: CancellationToken.None);

        Assert.Equal(CelErrorCode.ApplicationException, Assert.Single(result.Errors).Code);
    }

    [Theory]
    [InlineData("[dyn(1)].unwrapOpt()")]
    [InlineData("optional.unwrap([dyn(1)])")]
    [InlineData("optional.unwrap([optional.of(1), dyn(1)])")]
    public static void Unwrap_OfAnElementThatIsNoOptional_IsEvalNoMatchingOverload(string expression)
    {
        CelEvalResult<CelValue> result = Environment.Compile<CelValue>(expression).GetProgramOrThrow()
            .Evaluate(new CelBindings(), cancellationToken: CancellationToken.None);

        Assert.Equal(CelErrorCode.EvalNoMatchingOverload, Assert.Single(result.Errors).Code);
    }

    [Fact]
    public static void Implementations_OfAnArgumentThatNoOverloadTakes_AreNoMatchingOverload()
    {
        var site = new CallSite("value", 0, Lines);
        CelValue number = CelValue.FromInt64(1);
        CelValue numbers = CelValue.FromList(CelList.Create([number]));

        Assert.Equal(CelErrorCode.EvalNoMatchingOverload, ErrorOf(OptionalFunctions.Value([number], in site)).Code);
        Assert.Equal(CelErrorCode.EvalNoMatchingOverload, ErrorOf(OptionalFunctions.HasValue([number], in site)).Code);
        Assert.Equal(CelErrorCode.EvalNoMatchingOverload, ErrorOf(OptionalFunctions.First([number], in site)).Code);
        Assert.Equal(CelErrorCode.EvalNoMatchingOverload, ErrorOf(OptionalFunctions.Last([number], in site)).Code);
        Assert.Equal(CelErrorCode.EvalNoMatchingOverload, ErrorOf(OptionalFunctions.Unwrap([number], in site)).Code);
        Assert.Equal(CelErrorCode.EvalNoMatchingOverload, ErrorOf(OptionalFunctions.Unwrap([numbers], in site)).Code);
    }

    [Fact]
    public static void Value_OfAnEmptyOptional_IsOptionalHasNoValueAtTheCall()
    {
        var site = new CallSite("value", 0, Lines);

        CelError error = ErrorOf(OptionalFunctions.Value([CelValue.FromOptional(default)], in site));

        Assert.Equal(CelErrorCode.OptionalHasNoValue, error.Code);
        Assert.Equal("optional value is empty", error.Message);
        Assert.Equal(0, error.Location.Offset);
    }

    [Fact]
    public static void Unwrap_GivesANewListOfTheHeldValues_AndTheSameEmptyListWhenNothingIsHeld()
    {
        var site = new CallSite("unwrapOpt", 0, Lines);
        CelValue none = CelValue.FromOptional(default);
        CelValue ofNull = CelValue.FromOptional(new CelOptional<CelValue>(CelValue.Null));
        CelValue ofOne = CelValue.FromOptional(new CelOptional<CelValue>(CelValue.FromInt64(1)));

        CelValue held = OptionalFunctions.Unwrap([CelValue.FromList(CelList.Create([ofOne, none, ofNull]))], in site);
        CelValue nothing = OptionalFunctions.Unwrap([CelValue.FromList(CelList.Create([none, none]))], in site);
        CelValue empty = OptionalFunctions.Unwrap([CelValue.FromList(CelList.Create([]))], in site);

        Assert.True(held.TryGetList(out CelList? values));
        Assert.Equal(2, values.Count);
        Assert.Equal(CelValueKind.Int, values[0].Kind);
        Assert.Equal(CelValueKind.Null, values[1].Kind);
        Assert.True(nothing.TryGetList(out CelList? fromNothing));
        Assert.True(empty.TryGetList(out CelList? fromEmpty));
        Assert.Empty(fromNothing);
        Assert.Same(fromNothing, fromEmpty);
    }

    private static CelError ErrorOf(CelValue value)
    {
        Assert.True(value.TryGetError(out CelError? error));
        return error;
    }

    private sealed class Probe
    {
        internal bool First { get; init; }

        internal bool Second { get; init; }
    }

    // An object type of two fields; a field is set when its flag is.
    private sealed class ProbeType() : CelObjectType("acme.Probe", typeof(Probe), [new(0, "first", CelType.Bool), new(1, "second", CelType.Bool)])
    {
        internal static CelValue Bare(object instance) => FromInstance(instance);

        protected internal override CelValue GetField(object instance, int number) =>
            CelValue.FromBoolean(HasField(instance, number));

        protected internal override bool HasField(object instance, int number) =>
            number == 0 ? ((Probe)instance).First : ((Probe)instance).Second;
    }

    // A list of the application whose element cannot be read.
    private sealed class BrokenList : IReadOnlyList<long>
    {
        public int Count => 1;

        public long this[int index] => throw new InvalidOperationException("The element cannot be read.");

        public IEnumerator<long> GetEnumerator() => throw new NotSupportedException();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

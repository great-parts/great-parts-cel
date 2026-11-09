using System;
using System.Collections.Generic;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Values;

// The rows of time of the type mapping, as input and as result: DateTimeOffset, DateTime, TimeSpan, CelTimestamp,
// CelDuration and the Nullable<> of each; the refusals and the places that name them; and the depths at which a value of
// time comes through (a list element, a map value, a field, dyn, the result of a typed delegate).
public sealed class TimeConversionTests
{
    private static readonly DateTimeOffset Moment = new(2031, 7, 4, 9, 30, 15, TimeSpan.FromHours(-3));
    private static readonly CelTimestamp MomentStamp = new(1_940_934_615, 0);

    [Fact]
    public static void InputType_OfEveryRowOfTime_IsTheTypeOfTheValue()
    {
        Assert.Equal(
            ["timestamp", "timestamp", "timestamp", "duration", "duration", "timestamp", "timestamp", "timestamp", "duration", "duration"],
            new[]
            {
                Input<CelTimestamp>(), Input<DateTimeOffset>(), Input<DateTime>(), Input<CelDuration>(), Input<TimeSpan>(),
                Input<CelTimestamp?>(), Input<DateTimeOffset?>(), Input<DateTime?>(), Input<CelDuration?>(), Input<TimeSpan?>(),
            });
    }

    [Fact]
    public static void InputRows_ConvertTheMomentExactly_AndDropTheOffset()
    {
        Assert.True(ConversionRules.TryFromClr(Moment, out CelValue fromOffset));
        Assert.True(ConversionRules.TryFromClr(Moment.ToUniversalTime(), out CelValue fromUtc));
        Assert.Equal(fromUtc, fromOffset);
        Assert.True(fromOffset.TryGetTimestamp(out CelTimestamp stamp));
        Assert.Equal(MomentStamp, stamp);

        Assert.True(ConversionRules.TryFromClr(Moment.UtcDateTime, out CelValue fromDateTime));
        Assert.Equal(fromUtc, fromDateTime);
        Assert.True(ConversionRules.TryFromClr(new CelTimestamp(5, 6), out CelValue fromStamp));
        Assert.Equal(CelValue.FromTimestamp(new CelTimestamp(5, 6)), fromStamp);
        Assert.True(ConversionRules.TryFromClr(TimeSpan.FromTicks(-12_345), out CelValue fromSpan));
        Assert.Equal(CelValue.FromDuration(new CelDuration(-1_234_500)), fromSpan);
        Assert.True(ConversionRules.TryFromClr(new CelDuration(long.MinValue), out CelValue fromDuration));
        Assert.Equal(CelValue.FromDuration(new CelDuration(long.MinValue)), fromDuration);
    }

    [Fact]
    public static void InputRows_OfTheLimitsOfDateTimeOffset_AreInsideTheRangeOfTimestamp()
    {
        Assert.True(ConversionRules.TryFromClr(DateTimeOffset.MinValue, out CelValue min));
        Assert.True(ConversionRules.TryFromClr(DateTimeOffset.MaxValue, out CelValue max));
        Assert.True(min.TryGetTimestamp(out CelTimestamp first));
        Assert.True(max.TryGetTimestamp(out CelTimestamp last));
        Assert.Equal(("0001-01-01T00:00:00Z", "9999-12-31T23:59:59.9999999Z"), (first.ToString(), last.ToString()));
    }

    [Theory]
    [InlineData(DateTimeKind.Local, "Local")]
    [InlineData(DateTimeKind.Unspecified, "Unspecified")]
    public static void DateTime_OfAnotherKindThanUtc_IsAnErrorValueThatNamesItsKind(DateTimeKind kind, string kindName)
    {
        var value = new DateTime(2030, 1, 2, 3, 4, 5, kind);

        Assert.True(ConversionRules.TryFromClr(value, out CelValue result));
        Assert.True(ConversionRules.TryFromClr<DateTime?>(value, out CelValue nullable));
        Assert.True(ConversionRules.TryFromClr<object>(value, out CelValue boxed));

        foreach (CelValue refused in new[] { result, nullable, boxed })
        {
            AssertError(CelErrorCode.InvalidArgument, "a DateTime of the kind " + kindName + " is not an instant in UTC", refused);
        }

        AssertError(CelErrorCode.InvalidArgument, "a DateTime of the kind " + kindName + " is not an instant in UTC", ConversionRules.FromDateTime(value));
    }

    [Fact]
    public static void DateTime_OfTheKindUtc_IsAMoment_AtItsTicks()
    {
        var value = new DateTime(2030, 1, 2, 3, 4, 5, DateTimeKind.Utc).AddTicks(7);

        Assert.True(ConversionRules.TryFromClr(value, out CelValue result));
        Assert.True(result.TryGetTimestamp(out CelTimestamp stamp));
        Assert.Equal(700, stamp.Nanoseconds);
        Assert.Equal(CelTimestamp.FromDateTimeOffset(new DateTimeOffset(value)), stamp);
    }

    [Theory]
    [InlineData(long.MaxValue / 100 + 1)]
    [InlineData(long.MinValue / 100 - 1)]
    [InlineData(long.MaxValue)]
    [InlineData(long.MinValue)]
    public static void TimeSpan_OutsideTheRangeOfDuration_IsAnErrorValueWithTheTicks(long ticks)
    {
        var span = TimeSpan.FromTicks(ticks);

        Assert.True(ConversionRules.TryFromClr(span, out CelValue result));
        Assert.True(ConversionRules.TryFromClr<TimeSpan?>(span, out CelValue nullable));
        Assert.True(ConversionRules.TryFromClr<object>(span, out CelValue boxed));

        foreach (CelValue refused in new[] { result, nullable, boxed })
        {
            AssertError(CelErrorCode.ConversionRange, ticks + " ticks is out of the range of duration", refused);
        }
    }

    [Fact]
    public static void TimeSpan_AtTheEdgesOfTheRange_IsADuration()
    {
        Assert.True(ConversionRules.TryFromClr(TimeSpan.FromTicks(long.MaxValue / 100), out CelValue max));
        Assert.True(ConversionRules.TryFromClr(TimeSpan.FromTicks(long.MinValue / 100), out CelValue min));
        Assert.True(max.TryGetDuration(out CelDuration last));
        Assert.True(min.TryGetDuration(out CelDuration first));
        Assert.Equal((long.MaxValue / 100) * 100, last.TotalNanoseconds);
        Assert.Equal((long.MinValue / 100) * 100, first.TotalNanoseconds);
    }

    [Fact]
    public static void NullableOfTime_IsNullOrTheValue()
    {
        Assert.True(ConversionRules.TryFromClr<DateTimeOffset?>(null, out CelValue none));
        Assert.True(ConversionRules.TryFromClr<TimeSpan?>(null, out CelValue noSpan));
        Assert.True(ConversionRules.TryFromClr<CelTimestamp?>(null, out CelValue noStamp));
        Assert.True(ConversionRules.TryFromClr<CelDuration?>(null, out CelValue noDuration));
        Assert.True(ConversionRules.TryFromClr<DateTime?>(null, out CelValue noDate));
        Assert.All(new[] { none, noSpan, noStamp, noDuration, noDate }, value => Assert.Equal(CelValue.Null, value));

        Assert.True(ConversionRules.TryFromClr<DateTimeOffset?>(Moment, out CelValue some));
        Assert.Equal(CelValue.FromTimestamp(MomentStamp), some);
        Assert.True(ConversionRules.TryFromClr<TimeSpan?>(TimeSpan.FromSeconds(2), out CelValue span));
        Assert.Equal(CelValue.FromDuration(new CelDuration(2_000_000_000)), span);
    }

    [Fact]
    public static void ObjectRow_ConvertsABoxedValueOfTimeByItsRuntimeType()
    {
        object?[] boxed = [Moment, Moment.UtcDateTime, MomentStamp, TimeSpan.FromMilliseconds(1.5), new CelDuration(3)];
        CelValue[] expected =
        [
            CelValue.FromTimestamp(MomentStamp), CelValue.FromTimestamp(MomentStamp), CelValue.FromTimestamp(MomentStamp),
            CelValue.FromDuration(new CelDuration(1_500_000)), CelValue.FromDuration(new CelDuration(3)),
        ];

        for (int index = 0; index < boxed.Length; index++)
        {
            Assert.True(ConversionRules.TryFromClr(boxed[index], out CelValue value));
            Assert.Equal(expected[index], value);
        }
    }

    [Fact]
    public static void TypedCollectionOfTime_IsOutsideTheRows()
    {
        Assert.Null(ConversionRules.InputType<IReadOnlyList<DateTimeOffset>>());
        Assert.Null(ConversionRules.InputType<IReadOnlyDictionary<string, TimeSpan>>());
        Assert.False(ConversionRules.TryFromClr<IReadOnlyList<DateTimeOffset>>([Moment], out _));
        Assert.False(ConversionRules.TryFromClr<IReadOnlyDictionary<string, TimeSpan>>(new Dictionary<string, TimeSpan>(), out _));
        Assert.Throws<ArgumentException>(() => ConversionRules.ExpectedType<IReadOnlyList<DateTimeOffset>>(null, "options"));
        Assert.Throws<ArgumentException>(() => ConversionRules.ExpectedType<IReadOnlyDictionary<string, CelDuration>>(null, "options"));
        Assert.Throws<ArgumentException>(() => ConversionRules.ExpectedType<DateTime>(null, "options"));
        Assert.Throws<ArgumentException>(() => new CelBindings().Add<IReadOnlyList<DateTimeOffset>>("v", [Moment]));
        Assert.Throws<ArgumentException>(() => new CelBindings().Add<IReadOnlyDictionary<string, TimeSpan>>("v", new Dictionary<string, TimeSpan>()));
        Assert.Null(ConversionRules.ParameterType<DateTime>());
    }

    [Fact]
    public static void Bindings_AcceptTheRowsOfTime_AndRefuseADateTimeThatIsNotInUtc()
    {
        var bindings = new CelBindings()
            .Add("offset", Moment)
            .Add("utc", Moment.UtcDateTime)
            .Add("stamp", MomentStamp)
            .Add("span", TimeSpan.FromMinutes(1))
            .Add("duration", new CelDuration(5))
            .Add<DateTimeOffset?>("none", null)
            .Add<TimeSpan?>("some", TimeSpan.FromTicks(1));

        Assert.Equal(CelValue.FromTimestamp(MomentStamp), bindings.Values["offset"]);
        Assert.Equal(bindings.Values["offset"], bindings.Values["utc"]);
        Assert.Equal(bindings.Values["offset"], bindings.Values["stamp"]);
        Assert.Equal(CelValue.FromDuration(new CelDuration(60_000_000_000)), bindings.Values["span"]);
        Assert.Equal(CelValue.Null, bindings.Values["none"]);
        Assert.Equal(CelValue.FromDuration(new CelDuration(100)), bindings.Values["some"]);

        ArgumentException local = Assert.Throws<ArgumentException>(() => new CelBindings().Add("v", new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Local)));
        Assert.Contains("DateTime of the kind Local", local.Message, StringComparison.Ordinal);
        Assert.Equal("value", local.ParamName);
        Assert.Throws<ArgumentException>(() => new CelBindings().Add("v", new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)));
        Assert.Throws<ArgumentException>(() => new CelBindings().Add<DateTime?>("v", new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Local)));
        Assert.Throws<ArgumentException>(() => new CelBindings().Add<object>("v", new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)));
        Assert.Empty(new CelBindings().Values);
    }

    [Fact]
    public static void Bindings_OfATimeSpanOutsideTheRange_BindAnErrorValueThatTheReaderPlaces()
    {
        var bindings = new CelBindings().Add("span", TimeSpan.MaxValue);

        AssertError(CelErrorCode.ConversionRange, TimeSpan.MaxValue.Ticks + " ticks is out of the range of duration", bindings.Values["span"]);
    }

    // An element of a list and a value of a map of the application: a boxed value of time converts by its runtime type, and
    // a DateTime that is not in UTC is an error value that names the collection.
    [Fact]
    public static void ListElements_OfTheTypeObject_ConvertTimeAndNameARefusal()
    {
        IReadOnlyList<object> source =
        [
            Moment, MomentStamp, TimeSpan.FromSeconds(1), new CelDuration(9), new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Local), TimeSpan.MaxValue,
        ];

        Assert.True(ConversionRules.TryFromClr(source, out CelValue listValue));
        Assert.True(listValue.TryGetList(out CelList? list));

        Assert.Equal(CelValue.FromTimestamp(MomentStamp), list[0]);
        Assert.Equal(CelValue.FromTimestamp(MomentStamp), list[1]);
        Assert.Equal(CelValue.FromDuration(new CelDuration(1_000_000_000)), list[2]);
        Assert.Equal(CelValue.FromDuration(new CelDuration(9)), list[3]);
        AssertError(
            CelErrorCode.InvalidArgument,
            "invalid argument to 'a list of the application': a DateTime of the kind Local is not an instant in UTC",
            list[4]);
        AssertError(CelErrorCode.ConversionRange, TimeSpan.MaxValue.Ticks + " ticks is out of the range of duration", list[5]);
    }

    [Fact]
    public static void MapValues_OfTheTypeObject_ConvertTimeAndNameARefusal()
    {
        IReadOnlyDictionary<string, object> source = new Dictionary<string, object>
        {
            ["at"] = Moment,
            ["for"] = TimeSpan.FromHours(2),
            ["bad"] = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Unspecified),
        };

        Assert.True(ConversionRules.TryFromClr(source, out CelValue mapValue));
        Assert.True(mapValue.TryGetMap(out CelMap? map));

        Assert.True(map.TryGetValue(CelValue.FromString("at"), out CelValue at));
        Assert.Equal(CelValue.FromTimestamp(MomentStamp), at);
        Assert.True(map.TryGetValue(CelValue.FromString("for"), out CelValue span));
        Assert.Equal(CelValue.FromDuration(new CelDuration(7_200_000_000_000)), span);
        Assert.True(map.TryGetValue(CelValue.FromString("bad"), out CelValue bad));
        AssertError(
            CelErrorCode.InvalidArgument,
            "invalid argument to 'a map of the application': a DateTime of the kind Unspecified is not an instant in UTC",
            bad);
    }

    [Fact]
    public static void CreateView_WithAConverterOfTime_ReadsTimeThroughTheConverter()
    {
        CelList list = CelList.CreateView<DateTimeOffset>([Moment], static value => CelValue.FromTimestamp(CelTimestamp.FromDateTimeOffset(value)));

        Assert.Equal(CelValue.FromTimestamp(MomentStamp), list[0]);
    }

    [Fact]
    public static void FromResult_OfATypedDelegate_NamesTheOverloadForADateTimeThatIsNotInUtc()
    {
        CelValue refused = ConversionRules.FromResult(new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Local), "stamp_int", null);

        AssertError(CelErrorCode.InvalidArgument, "invalid argument to 'stamp_int': a DateTime of the kind Local is not an instant in UTC", refused);
        Assert.Equal(CelValue.FromTimestamp(MomentStamp), ConversionRules.FromResult(Moment.UtcDateTime, "stamp_int", null));
        Assert.Equal(CelValue.FromTimestamp(MomentStamp), ConversionRules.FromResult(Moment, "stamp_int", null));
        Assert.Equal(CelValue.FromDuration(new CelDuration(100)), ConversionRules.FromResult(TimeSpan.FromTicks(1), "span_int", null));
        Assert.Equal(CelValue.Null, ConversionRules.FromResult<TimeSpan?>(null, "span_int", null));
        AssertError(
            CelErrorCode.ConversionRange,
            TimeSpan.MinValue.Ticks + " ticks is out of the range of duration",
            ConversionRules.FromResult(TimeSpan.MinValue, "span_int", null));
    }

    [Fact]
    public static void ExpectedType_OfTheRowsOfTime_IsTheTypeOfTheValue()
    {
        Assert.Equal(
            ["timestamp", "timestamp", "timestamp", "duration", "duration", "duration"],
            new[]
            {
                Expected<CelTimestamp>(), Expected<DateTimeOffset>(), Expected<DateTimeOffset?>(),
                Expected<CelDuration>(), Expected<TimeSpan>(), Expected<CelDuration?>(),
            });
        Assert.Equal(CelType.Timestamp, ConversionRules.ExpectedType<DateTimeOffset>(CelType.Timestamp, "options"));
        Assert.Equal(CelType.Dyn, ConversionRules.ExpectedType<object>(CelType.Dyn, "options"));
        Assert.Throws<ArgumentException>(() => ConversionRules.ExpectedType<DateTimeOffset>(CelType.Duration, "options"));
        Assert.Throws<ArgumentException>(() => ConversionRules.ExpectedType<TimeSpan>(CelType.Int, "options"));
    }

    [Fact]
    public static void Result_ConvertsATimestampAndADurationToTheirRows()
    {
        var stamp = CelValue.FromTimestamp(MomentStamp);
        var span = CelValue.FromDuration(new CelDuration(1_500_000_000));

        Assert.True(ConversionRules.TryToResult(stamp, CelType.Timestamp, out CelTimestamp asStamp, out _, out _));
        Assert.Equal(MomentStamp, asStamp);
        Assert.True(ConversionRules.TryToResult(stamp, CelType.Timestamp, out DateTimeOffset asOffset, out _, out _));
        Assert.Equal(Moment.ToUniversalTime(), asOffset);
        Assert.Equal(TimeSpan.Zero, asOffset.Offset);
        Assert.True(ConversionRules.TryToResult(span, CelType.Duration, out CelDuration asDuration, out _, out _));
        Assert.Equal(1_500_000_000L, asDuration.TotalNanoseconds);
        Assert.True(ConversionRules.TryToResult(span, CelType.Duration, out TimeSpan asSpan, out _, out _));
        Assert.Equal(TimeSpan.FromSeconds(1.5), asSpan);
        Assert.True(ConversionRules.TryToResult(stamp, CelType.Timestamp, out DateTimeOffset? nullableOffset, out _, out _));
        Assert.Equal(Moment.ToUniversalTime(), nullableOffset);
        Assert.True(ConversionRules.TryToResult(span, CelType.Duration, out TimeSpan? nullableSpan, out _, out _));
        Assert.Equal(TimeSpan.FromSeconds(1.5), nullableSpan);
    }

    // The nanoseconds that are not a multiple of 100 do not fit the ticks of DateTimeOffset and TimeSpan: a refusal that
    // names the short type, never a rounding; the structures of the library take every value.
    [Fact]
    public static void Result_RefusesNanosecondsThatTicksCannotHold_AndNamesTheShortType()
    {
        var stamp = CelValue.FromTimestamp(new CelTimestamp(10, 150));
        var span = CelValue.FromDuration(new CelDuration(1));

        Assert.False(ConversionRules.TryToResult(stamp, CelType.Timestamp, out DateTimeOffset _, out string? stampType, out _));
        Assert.Equal("timestamp", stampType);
        Assert.False(ConversionRules.TryToResult(stamp, CelType.Timestamp, out DateTimeOffset? _, out _, out _));
        Assert.False(ConversionRules.TryToResult(span, CelType.Duration, out TimeSpan _, out string? spanType, out _));
        Assert.Equal("duration", spanType);
        Assert.False(ConversionRules.TryToResult(span, CelType.Duration, out TimeSpan? _, out _, out _));
        Assert.True(ConversionRules.TryToResult(stamp, CelType.Timestamp, out CelTimestamp kept, out _, out _));
        Assert.Equal(new CelTimestamp(10, 150), kept);
        Assert.True(ConversionRules.TryToResult(span, CelType.Duration, out CelDuration keptSpan, out _, out _));
        Assert.Equal(1L, keptSpan.TotalNanoseconds);
    }

    [Fact]
    public static void Result_OfAnotherKind_IsRefusedAndNamesTheKind()
    {
        Assert.False(ConversionRules.TryToResult(CelValue.FromInt64(5), CelType.Dyn, out CelTimestamp _, out string? actual, out _));
        Assert.Equal("int", actual);
        Assert.False(ConversionRules.TryToResult(CelValue.FromTimestamp(default), CelType.Dyn, out CelDuration _, out string? timestampType, out _));
        Assert.Equal("timestamp", timestampType);
        Assert.False(ConversionRules.TryToResult(CelValue.FromDuration(default), CelType.Dyn, out DateTimeOffset _, out string? durationType, out _));
        Assert.Equal("duration", durationType);
        Assert.False(ConversionRules.TryToResult(CelValue.Null, CelType.Dyn, out TimeSpan _, out string? nullType, out _));
        Assert.Equal("null_type", nullType);
    }

    [Fact]
    public static void Result_OfNullAsANullableOfTime_IsNull()
    {
        Assert.True(ConversionRules.TryToResult(CelValue.Null, CelType.Dyn, out DateTimeOffset? offset, out _, out _));
        Assert.Null(offset);
        Assert.True(ConversionRules.TryToResult(CelValue.Null, CelType.Dyn, out TimeSpan? span, out _, out _));
        Assert.Null(span);
    }

    [Fact]
    public static void Result_AsObject_GivesTheStructuresOfTheLibrary_AtAnyDepth()
    {
        var stamp = CelValue.FromTimestamp(new CelTimestamp(10, 7));
        var span = CelValue.FromDuration(new CelDuration(3));

        Assert.True(ConversionRules.TryToResult(stamp, CelType.Dyn, out object? boxedStamp, out _, out _));
        Assert.Equal(new CelTimestamp(10, 7), boxedStamp);
        Assert.True(ConversionRules.TryToResult(span, CelType.Dyn, out object? boxedSpan, out _, out _));
        Assert.Equal(new CelDuration(3), boxedSpan);

        CelValue list = CelValue.FromList(CelList.Create(stamp, span));
        Assert.True(ConversionRules.TryToResult(list, CelType.ListOf(CelType.Dyn), out IReadOnlyList<object?>? objects, out _, out _));
        Assert.Equal(new object[] { new CelTimestamp(10, 7), new CelDuration(3) }, objects);

        CelValue map = CelValue.FromMap(CelMap.Create([new KeyValuePair<CelValue, CelValue>(CelValue.FromString("k"), stamp)]));
        Assert.True(ConversionRules.TryToResult(map, CelType.MapOf(CelType.String, CelType.Dyn), out IReadOnlyDictionary<string, object?>? dictionary, out _, out _));
        Assert.Equal(new CelTimestamp(10, 7), dictionary!["k"]);
    }

    [Fact]
    public static void Parameter_OfATypedDelegate_ConvertsFromTheValueThatFitsTheDeclaredType()
    {
        Assert.True(ConversionRules.TryToParameter(CelValue.FromTimestamp(MomentStamp), null, out DateTimeOffset offset));
        Assert.Equal(Moment.ToUniversalTime(), offset);
        Assert.True(ConversionRules.TryToParameter(CelValue.FromTimestamp(MomentStamp), null, out CelTimestamp stamp));
        Assert.Equal(MomentStamp, stamp);
        Assert.True(ConversionRules.TryToParameter(CelValue.FromDuration(new CelDuration(-200)), null, out TimeSpan span));
        Assert.Equal(TimeSpan.FromTicks(-2), span);
        Assert.True(ConversionRules.TryToParameter(CelValue.FromDuration(new CelDuration(long.MinValue)), null, out CelDuration duration));
        Assert.Equal(long.MinValue, duration.TotalNanoseconds);

        Assert.False(ConversionRules.TryToParameter(CelValue.FromTimestamp(new CelTimestamp(1, 1)), null, out DateTimeOffset _));
        Assert.False(ConversionRules.TryToParameter(CelValue.FromDuration(new CelDuration(1)), null, out TimeSpan _));
    }

    [Fact]
    public static void Fields_OfAnObjectType_ConvertDateTimeAndTimeSpanAndNameTheField()
    {
        var type = new ObjectTypeTests.OrderType("acme.Order", [new(0, "created", CelType.Timestamp), new(1, "age", CelType.Duration)]);

        Assert.Equal(CelValue.FromTimestamp(MomentStamp), type.DateTimeField(0, Moment.UtcDateTime));
        Assert.Equal(CelValue.FromDuration(new CelDuration(2_000_000_000)), type.TimeSpanField(1, TimeSpan.FromSeconds(2)));
        AssertError(
            CelErrorCode.InvalidArgument,
            "invalid argument to 'acme.Order.created': a DateTime of the kind Local is not an instant in UTC",
            type.DateTimeField(0, new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Local)));
        AssertError(
            CelErrorCode.ConversionRange,
            TimeSpan.MaxValue.Ticks + " ticks is out of the range of duration",
            type.TimeSpanField(1, TimeSpan.MaxValue));
        AssertError(
            CelErrorCode.InvalidArgument,
            "invalid argument to 'acme.Order.created': a DateTime of the kind Unspecified is not an instant in UTC",
            type.Dynamic(0, new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)));
        Assert.Equal(CelValue.FromTimestamp(MomentStamp), type.Dynamic(0, Moment));
        Assert.Equal(CelValue.FromDuration(new CelDuration(100)), type.Dynamic(1, TimeSpan.FromTicks(1)));
    }

    private static string Input<T>() => ConversionRules.InputType<T>()!.ToString();

    private static string Expected<T>() => ConversionRules.ExpectedType<T>(null, "options").ToString();

    private static void AssertError(CelErrorCode code, string message, CelValue value)
    {
        Assert.True(value.TryGetError(out CelError? error));
        Assert.NotNull(error);
        Assert.Equal((code, message), (error.Code, error.Message));
    }
}

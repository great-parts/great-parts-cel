using System;
using System.Collections.Generic;
using System.Text.Json;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Generator.Samples.Rows;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Tests.Examples.Generated.GeneratedExample;

namespace Great.Parts.Cel.Tests.Examples.Generated;

// Every input row of the type mapping read from a member of a type that the generator described (the sample class Scalars, whose
// members are named after their rows). The member names are the camel-case names of the default policy. A selection reads the
// member by its field number in the created code; has() is "the member is not null"; a null that the CEL type does not allow
// is UnexpectedNull at the selection, whose place is the "." of the selection (the second character of `s.x`).
public sealed class GeneratedRowTests
{
    private static readonly DateTimeOffset Moment = new(2026, 11, 1, 12, 0, 0, TimeSpan.FromHours(3));

    private static readonly CelEnvironment Environment = Builder(ScalarContext.Default)
        .UseContainer(Samples + ".Rows")
        .AddVariable<Scalars>("s")
        .AddVariable<Oblivious>("o")
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "P-2")]
    public void Numbers_AreReadAsTheirCelTypes()
    {
        CelBindings bindings = new CelBindings().Add("s", Full());

        Assert.True(Evaluate<bool>(Environment, "s.int64 == -5 && s.int32 == 6 && s.int16 == 7 && s.sbyte == -8 && s.nint == 9", bindings));
        Assert.True(Evaluate<bool>(Environment, "s.uint64 == 18446744073709551615u && s.uint32 == 4294967295u && s.uint16 == 10u && s.byte == 11u && s.nuint == 12u", bindings));
        Assert.True(Evaluate<bool>(Environment, "s.double == 0.5 && s.float == 1.5 && s.half == 2.5 && s.bool", bindings));
        Assert.True(Evaluate<bool>(Environment, "type(s.int16) == int && type(s.uint16) == uint && type(s.float) == double && type(s.nint) == int", bindings));
    }

    // A string that is not null is string, a Guid is its text of format D in lower case, an enumeration is int with its
    // constants as <CEL name>.<member>: Status.Approved here, by the container of the namespace of the sample.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void TextsAndEnumerations_AreReadAsTheirCelTypes()
    {
        CelBindings bindings = new CelBindings().Add("s", Full());

        Assert.True(Evaluate<bool>(Environment, "s.text == 'héllo' && s.text.size() == 5", bindings));
        Assert.True(Evaluate<bool>(Environment, "s.id == '0f8fad5b-d9cb-469f-a165-70867728950e'", bindings));
        Assert.True(Evaluate<bool>(Environment, "s.status == Status.Approved && s.permissions == 3 && s.wide == Wide.Small", bindings));
        Assert.True(Evaluate<bool>(Environment, "!has(s.note) && s.note == null && has(s.text)", bindings));
        Assert.True(Evaluate<bool>(Environment, "type(s.text) == string && type(s.status) == int", bindings));
    }

    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-6")]
    public void BytesAndTimes_AreReadAsTheirCelTypes()
    {
        CelBindings bindings = new CelBindings().Add("s", Full());

        Assert.True(Evaluate<bool>(Environment, @"s.blob == b'\x01\x02\x03' && s.memory == b'\x04\x05' && s.blob.size() == 3", bindings));
        Assert.True(Evaluate<bool>(Environment, "!has(s.optBlob) && s.optBlob == null", bindings));
        Assert.True(Evaluate<bool>(Environment, "s.moment == timestamp('2026-11-01T09:00:00Z') && s.stamp == timestamp('2026-11-01T09:00:00Z')", bindings));
        Assert.True(Evaluate<bool>(Environment, "s.instant == timestamp('2026-11-01T09:00:00Z')", bindings));
        Assert.True(Evaluate<bool>(Environment, "s.lapse == duration('1h') && s.elapsed == duration('5400s')", bindings));
        Assert.True(Evaluate<bool>(Environment, "type(s.moment) == google.protobuf.Timestamp && type(s.elapsed) == google.protobuf.Duration", bindings));
    }

    // Nullable<T> of a type that has a wrapper in CEL is that wrapper: null is the null value and has() is false; of a type
    // without one (DateTimeOffset?, TimeSpan?) it is the type of T and null is UnexpectedNull; of a structure of the context it is the
    // object type with null as the null value.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void NullableMembers_AreWrappersOrTheirUnderlyingType()
    {
        Scalars empty = Full();
        Scalars set = Full();
        set.OptInt = 5;
        set.OptStatus = Status.Draft;
        set.OptId = Guid.Empty;
        set.OptMoment = Moment;
        set.OptElapsed = TimeSpan.FromSeconds(2);
        set.OptPoint = new Point { X = 3, Y = 4 };

        Assert.True(Evaluate<bool>(Environment, "!has(s.optInt) && s.optInt == null && !has(s.optStatus) && s.optStatus == null && !has(s.optId) && s.optId == null", new CelBindings().Add("s", empty)));
        Assert.True(Evaluate<bool>(Environment, "!has(s.optPoint) && s.optPoint == null && !has(s.optMoment) && !has(s.optElapsed)", new CelBindings().Add("s", empty)));
        Assert.True(Evaluate<bool>(Environment, "s.optInt == 5 && s.optStatus == 0 && s.optId == '00000000-0000-0000-0000-000000000000'", new CelBindings().Add("s", set)));
        Assert.True(Evaluate<bool>(Environment, "s.optMoment == timestamp('2026-11-01T09:00:00Z') && s.optElapsed == duration('2s')", new CelBindings().Add("s", set)));
        Assert.True(Evaluate<bool>(Environment, "has(s.optPoint) && s.optPoint.x == 3 && s.optPoint.y == 4", new CelBindings().Add("s", set)));
        AssertError(EvaluationError(Environment, "s.optMoment", new CelBindings().Add("s", empty)), CelErrorCode.UnexpectedNull, 1);
        AssertError(EvaluationError(Environment, "s.optElapsed", new CelBindings().Add("s", empty)), CelErrorCode.UnexpectedNull, 1);
    }

    // object is dyn: the value by its own type; a CelValue as it is; CelList and CelMap as list and map; a JsonElement by its kind.
    // A CelList and a CelMap are list(dyn) and map(dyn, dyn), types that take no null: a null member is UnexpectedNull at the selection
    // and has() is false, as for every collection.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-6")]
    public void DynamicMembers_AreReadByTheirValues()
    {
        Scalars scalars = Full();
        scalars.Any = "text";
        scalars.Value = CelValue.FromInt64(7);
        scalars.Items = CelList.Create(CelValue.FromInt64(1), CelValue.FromInt64(2));
        scalars.Table = CelMap.Create([new KeyValuePair<CelValue, CelValue>(CelValue.FromString("k"), CelValue.FromInt64(1))]);
        scalars.Json = JsonDocument.Parse("""{"a": 1, "b": [true, null]}""").RootElement;
        CelBindings bindings = new CelBindings().Add("s", scalars);

        Assert.True(Evaluate<bool>(Environment, "s.any == 'text' && has(s.any) && s.always == ''", bindings));
        Assert.True(Evaluate<bool>(Environment, "s.value == 7 && size(s.items) == 2 && s.items[1] == 2 && s.table.k == 1", bindings));
        Assert.True(Evaluate<bool>(Environment, "s.json.a == 1.0 && s.json.b[0] == true && s.json.b[1] == null", bindings));
        Assert.True(Evaluate<bool>(Environment, "has(s.items) && has(s.table) && has(s.any)", bindings));

        Scalars nulls = Full();
        Assert.True(Evaluate<bool>(Environment, "!has(s.any) && s.any == null && !has(s.items) && !has(s.table)", new CelBindings().Add("s", nulls)));
        AssertError(EvaluationError(Environment, "s.items", new CelBindings().Add("s", nulls)), CelErrorCode.UnexpectedNull, 1);
        AssertError(EvaluationError(Environment, "s.table", new CelBindings().Add("s", nulls)), CelErrorCode.UnexpectedNull, 1);
    }

    // The errors of a read: the value that no CEL type can hold, and the null that the type does not allow. The place of each is
    // the selection.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-4")]
    public void ValuesThatCannotBeRead_AreErrorsAtTheSelection()
    {
        Scalars huge = Full();
        huge.Wide = Wide.Huge;
        Scalars local = Full();
        local.Instant = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Local);
        Scalars long_ = Full();
        long_.Elapsed = TimeSpan.MaxValue;
        Scalars nulls = Full();
        nulls.Text = null!;
        nulls.Always = null!;
        nulls.Blob = null!;

        AssertError(EvaluationError(Environment, "s.wide", new CelBindings().Add("s", huge)), CelErrorCode.ConversionRange, 1, "18446744073709551615 is out of the range of int");
        AssertError(EvaluationError(Environment, "s.instant", new CelBindings().Add("s", local)), CelErrorCode.InvalidArgument, 1);
        AssertError(EvaluationError(Environment, "s.elapsed", new CelBindings().Add("s", long_)), CelErrorCode.ConversionRange, 1);
        AssertError(EvaluationError(Environment, "s.text", new CelBindings().Add("s", nulls)), CelErrorCode.UnexpectedNull, 1);
        AssertError(EvaluationError(Environment, "s.always", new CelBindings().Add("s", nulls)), CelErrorCode.UnexpectedNull, 1);
        AssertError(EvaluationError(Environment, "s.blob", new CelBindings().Add("s", nulls)), CelErrorCode.UnexpectedNull, 1);
        Assert.True(Evaluate<bool>(Environment, "!has(s.text) && !has(s.always) && !has(s.blob)", new CelBindings().Add("s", nulls)));
        Assert.True(Evaluate<bool>(Environment, "s.wide == Wide.Small", new CelBindings().Add("s", Full())));
    }

    // The constants of an enumeration that the type of the member names: the one over the range of int64 is not among them,
    // so the CEL name of it is not an identifier of the environment.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void ConstantOverTheRangeOfInt64_IsNotInTheDescriptionOfTheEnumeration()
    {
        CelCompileResult<bool> huge = Environment.Compile<bool>("s.wide == Wide.Huge");

        Assert.False(huge.IsSuccess);
        Assert.NotEmpty(huge.Errors);
        Assert.True(Evaluate<bool>(Environment, "Wide.Small == 1", new CelBindings().Add("s", Full())));
    }

    // The project without the nullable context: a reference type with no annotation is read as nullable, so a null is the null
    // value and has() is false; a type of the context in such a member is the object type that takes null.
    [Fact]
    [Trait("Requirement", "F-2")]
    public void ObliviousMembers_ReadNullAsTheNullValue()
    {
        var oblivious = new Oblivious { Text = null, Blob = null, Any = null, Inner = null };
        var set = new Oblivious { Text = "t", Blob = [1], Any = 5L, Inner = Full() };

        Assert.True(Evaluate<bool>(Environment, "!has(o.text) && o.text == null && !has(o.blob) && o.blob == null && !has(o.any) && o.any == null && !has(o.inner) && o.inner == null", new CelBindings().Add("o", oblivious)));
        Assert.True(Evaluate<bool>(Environment, "o.text == 't' && o.blob == b'\\x01' && o.any == 5 && o.inner.int64 == -5", new CelBindings().Add("o", set)));
    }

    private static Scalars Full() => new()
    {
        Int64 = -5,
        Int32 = 6,
        Int16 = 7,
        Sbyte = -8,
        Nint = 9,
        Uint64 = ulong.MaxValue,
        Uint32 = uint.MaxValue,
        Uint16 = 10,
        Byte = 11,
        Nuint = 12,
        Double = 0.5,
        Float = 1.5f,
        Half = (Half)2.5,
        Bool = true,
        Text = "héllo",
        Id = new Guid("0F8FAD5B-D9CB-469F-A165-70867728950E"),
        Status = Status.Approved,
        Permissions = Permissions.Read | Permissions.Write,
        Wide = Wide.Small,
        Blob = [1, 2, 3],
        Memory = new byte[] { 4, 5 },
        Stamp = CelTimestamp.FromDateTimeOffset(Moment),
        Moment = Moment,
        Instant = new DateTime(2026, 11, 1, 9, 0, 0, DateTimeKind.Utc),
        Lapse = CelDuration.FromTimeSpan(TimeSpan.FromHours(1)),
        Elapsed = TimeSpan.FromMinutes(90),
    };
}

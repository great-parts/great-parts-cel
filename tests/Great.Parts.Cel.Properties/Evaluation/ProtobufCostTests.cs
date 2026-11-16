using System.Globalization;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Great.Parts.Cel.Properties.Messages;
using Great.Parts.Cel.Protobuf;
using Great.Parts.Cel.Values;
using Xunit;
using static Great.Parts.Cel.Properties.Evaluation.CostRows;
using Proto3 = global::Cel.Expr.Conformance.Proto3;

namespace Great.Parts.Cel.Properties.Evaluation;

// The memory of the messages of protobuf (docs/design/tests.md, "Cost and memory of the extension sets" and the section of the
// protobuf package): the bytes that an evaluation allocates beyond its frame are at most the units it charges times B, for the read
// of a field of each kind, the views of repeated and map fields, the unpacking of an Any, a literal of a small message and the views
// of Struct, Value and ListValue. The rows run on the sizes of CostRows through Compile and through the planner
// without type checking; an Any has its own sizes, up to 10 000 bytes of content.
public sealed class ProtobufCostTests
{
    private const string Container = "cel.expr.conformance.proto3.";

    // The sizes of the content of an Any.
    private static readonly int[] AnySizes = [0, 10, 1000, 10000];

    private static readonly CelProtobufTypes Messages = new CelProtobufTypesBuilder("test messages")
        .AddFile(Proto3.TestAllTypesReflection.Descriptor)
        .AddFile(MemoryRowsReflection.Descriptor)
        .Build();

    // The messages of protobuf and the set protos.
    private static readonly CelEnvironment MessagesEnvironment = Dynamic(new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Protos(0))
        .AddTypes(Messages));

    private static readonly CostTable Table = new CostTable(MessagesEnvironment)
        // The read of a field of each kind.
        .Add("protobuf, field, int64", _ => Message("x.single_int64", new Proto3.TestAllTypes { SingleInt64 = 5 }))
        .Add("protobuf, field, string", size => Message("x.single_string", new Proto3.TestAllTypes { SingleString = new string('a', size) }))
        .Add("protobuf, field, bytes", size => Message("x.single_bytes", new Proto3.TestAllTypes { SingleBytes = ByteString.CopyFrom(new byte[size]) }))
        .Add("protobuf, field, message", _ => Message("x.standalone_message", new Proto3.TestAllTypes { StandaloneMessage = new Proto3.TestAllTypes.Types.NestedMessage { Bb = 1 } }))
        .Add("protobuf, field, enum", _ => Message("x.single_nested_enum", new Proto3.TestAllTypes { SingleNestedEnum = Proto3.TestAllTypes.Types.NestedEnum.Bar }))
        .Add("protobuf, field, wrapper", _ => Message("x.single_int64_wrapper", new Proto3.TestAllTypes { SingleInt64Wrapper = 5 }))
        .Add("protobuf, field, Timestamp", _ => Message("x.single_timestamp", new Proto3.TestAllTypes { SingleTimestamp = new Timestamp { Seconds = 5, Nanos = 6 } }))
        .Add("protobuf, field, Duration", _ => Message("x.single_duration", new Proto3.TestAllTypes { SingleDuration = new Duration { Seconds = 5, Nanos = 6 } }))

        // The views of repeated and map fields: the read and the traversal.
        .Add("protobuf, repeated field", size => Message("x.repeated_int64", WithRepeated(size)))
        .Add("protobuf, repeated field, traversal", size => Message("x.repeated_int64.all(v, v >= 0)", WithRepeated(size)))
        .Add("protobuf, repeated message field, traversal", size => Message("x.repeated_nested_message.exists(m, m.bb < 0)", WithNestedMessages(size)))
        .Add("protobuf, map field", size => Message("x.map_string_int64", WithRepeated(size)))
        // The traversal of a map field is calibrated at 62.7 bytes per unit of B = 64 (docs/cost.md), and it was the one row to fail in
        // the initial run of Stryker.NET, on its Debug build with the injected code: the row is measured on the optimized build only.
        .Add("protobuf, map field, traversal", size => Message("x.map_string_int64.all(k, k != 'zz')", WithRepeated(size)), optimizedOnly: true)
        .Add("protobuf, map field, in", size => Message("'0' in x.map_string_int64", WithRepeated(size)))

        // The unpacking of an Any with content of 0, 10, 1 000 and 10 000 bytes, and a repeated field of Any.
        .Add("protobuf, Any with bytes", size => Message("x.single_any", new Proto3.TestAllTypes { SingleAny = Any.Pack(new BytesValue { Value = ByteString.CopyFrom(new byte[size]) }) }), sizes: AnySizes)
        .Add("protobuf, Any with a string", size => Message("x.single_any", new Proto3.TestAllTypes { SingleAny = Any.Pack(new StringValue { Value = new string('a', size) }) }), sizes: AnySizes)
        .Add("protobuf, repeated Any", size => Message("x.repeated_any", WithAnys(size)))

        // A literal: of a message of one field, and of a small message of the tests with a value of bytes of the size. TestAllTypes is
        // not used for the second, nor is MemoryRow: the instance of a class allocates its repeated and map fields when it is made,
        // 27 904 bytes for TestAllTypes and a few hundred for MemoryRow, which the literal charge of 1 + n + ⌈L/10⌉ does not cover.
        .Add("protobuf, literal of a small message", _ => With($"{Container}TestAllTypes.NestedMessage{{bb: 1}}", CelValue.FromInt64(0)))
        .Add("protobuf, literal with a bytes value", size => With("memory.rows.MemoryBytes{data: x}", CelValue.FromBytes(new byte[size])))

        // The equality of two small messages, a repeated field and a map field of the size in each. TestAllTypes is not used: the
        // reflection of Google.Protobuf boxes the value of each of its sixty fields that a comparison tests.
        .Add("protobuf, equality of two messages", size =>
        {
            var bindings = new CelBindings();
            bindings.Add("x", MemoryRowOf(size));
            bindings.Add("p", MemoryRowOf(size));
            return ("x == p", bindings);
        })

        // The views of Struct, Value and ListValue.
        .Add("protobuf, Struct", size => Message("x.single_struct", new Proto3.TestAllTypes { SingleStruct = StructOf(size) }))
        .Add("protobuf, Struct, traversal", size => Message("x.single_struct.all(k, k != 'zz')", new Proto3.TestAllTypes { SingleStruct = StructOf(size) }))
        .Add("protobuf, Value with a string", size => Message("x.single_value", new Proto3.TestAllTypes { SingleValue = Value.ForString(new string('a', size)) }))
        .Add("protobuf, ListValue", size => Message("x.list_value", new Proto3.TestAllTypes { ListValue = ListOf(size) }));

    public static TheoryData<string> RowNames() => Table.Names();

    [Theory]
    [Trait("Requirement", "R-3")]
    [MemberData(nameof(RowNames))]
    public static void Row_AllocatesWithinCost(string row) => Table.Check(row);

    private static (string, CelBindings) Message(string text, Proto3.TestAllTypes message)
    {
        var bindings = new CelBindings();
        bindings.Add("x", message);
        return (text, bindings);
    }

    // The elements 0, 1, … of the size in the repeated field and the keys "0", "1", … in the map field, each with the value index.
    private static Proto3.TestAllTypes WithRepeated(int size)
    {
        var message = new Proto3.TestAllTypes();
        for (int index = 0; index < size; index++)
        {
            message.RepeatedInt64.Add(index);
            message.MapStringInt64[index.ToString(CultureInfo.InvariantCulture)] = index;
        }

        return message;
    }

    // A small message with the items 0, 1, … and the counts "0", "1", … of the size, and a name and a value of bytes.
    private static MemoryRow MemoryRowOf(int size)
    {
        var message = new MemoryRow { Name = "row", Data = ByteString.CopyFrom(new byte[size]) };
        for (int index = 0; index < size; index++)
        {
            message.Items.Add(index);
            message.Counts[index.ToString(CultureInfo.InvariantCulture)] = index;
        }

        return message;
    }

    private static Proto3.TestAllTypes WithNestedMessages(int size)
    {
        var message = new Proto3.TestAllTypes();
        for (int index = 0; index < size; index++)
        {
            message.RepeatedNestedMessage.Add(new Proto3.TestAllTypes.Types.NestedMessage { Bb = index });
        }

        return message;
    }

    // Any messages of ten bytes each.
    private static Proto3.TestAllTypes WithAnys(int size)
    {
        var message = new Proto3.TestAllTypes();
        for (int index = 0; index < size; index++)
        {
            message.RepeatedAny.Add(Any.Pack(new BytesValue { Value = ByteString.CopyFrom(new byte[10]) }));
        }

        return message;
    }

    private static Struct StructOf(int size)
    {
        var value = new Struct();
        for (int index = 0; index < size; index++)
        {
            value.Fields["k" + index.ToString(CultureInfo.InvariantCulture)] = Value.ForNumber(index);
        }

        return value;
    }

    private static ListValue ListOf(int size)
    {
        var value = new ListValue();
        for (int index = 0; index < size; index++)
        {
            value.Values.Add(Value.ForNumber(index));
        }

        return value;
    }
}

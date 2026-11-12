using System;
using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using static Great.Parts.Cel.Tests.Evaluation.ObjectSamples;

namespace Great.Parts.Cel.Tests.Evaluation;

// A constructible type of the tests, which stands for a protobuf message without the package: acme.test.Message has a field of
// each kind a literal can write, and the descriptions of three well-known messages — google.protobuf.Int32Value, Value and Any —
// that the literal of a known type and a declaration of the type Any need. Each description records the calls of CreateInstance.
internal static class MessageSamples
{
    internal const string Name = "acme.test.Message";

    internal const int Id = 0;
    internal const int Label = 1;
    internal const int Ratio = 2;
    internal const int Blob = 3;
    internal const int Tags = 4;
    internal const int Child = 5;
    internal const int Count = 6;
    internal const int Extra = 7;
    internal const int Payload = 8;
    internal const int Small = 9;
    internal const int Attributes = 10;
    internal const int Extension = 11;

    // The fields in the order of their numbers: id, label, ratio, blob, tags, child, count, extra, payload, small, attributes and the
    // extension pkg.count_ext, a field whose name has a dot, as the extension of a message has.
    private static readonly CelObjectField[] MessageFields =
    [
        new(Id, "id", CelType.Int),
        new(Label, "label", CelType.String),
        new(Ratio, "ratio", CelType.Double),
        new(Blob, "blob", CelType.Bytes),
        new(Tags, "tags", CelType.ListOf(CelType.String)),
        new(Child, "child", CelType.ObjectNamed(Name)),
        new(Count, "count", CelType.WrapperOf(CelType.Int)),
        new(Extra, "extra", CelType.Dyn),
        new(Payload, "payload", CelType.Any),
        new(Small, "small", CelType.Int),
        new(Attributes, "attributes", CelType.MapOf(CelType.String, CelType.Int)),
        new(Extension, "pkg.count_ext", CelType.Int),
    ];

    // The environment: the description of the message and the three known types, a variable msg of the message, anything of the type
    // Any and d of the type dyn, and the set protos; with a container when one is given and the descriptions of other types when there are some.
    internal static CelEnvironment CreateEnvironment(MessageType message, string container = "", params CelObjectType[] others)
    {
        CelEnvironmentBuilder builder = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddTypes(new SampleProvider("test", [message, KnownInt32(), KnownValue(), KnownAny()], []))
            .AddVariable("msg", message.Type)
            .AddVariable("anything", CelType.Any)
            .AddVariable("d", CelType.Dyn)
            .AddExtension(CelExtensions.Protos(0));
        if (others.Length > 0)
        {
            builder = builder.AddTypes(new SampleProvider("others", others, []));
        }

        return (container.Length == 0 ? builder : builder.UseContainer(container)).Build().GetEnvironmentOrThrow();
    }

    // The description of the message that creates instances itself, or by the creator when one is given: it gets the numbers and the
    // values and returns the value of the literal.
    internal static MessageType NewMessageType(Func<MessageType, int[], CelValue[], CelValue>? creator = null, Func<int, long>? readCost = null) =>
        new(Name, typeof(Message), MessageFields, creator, readCost);

    internal static CelObjectType KnownInt32() => new KnownType(
        "google.protobuf.Int32Value",
        typeof(Int32Marker),
        [new(0, "value", CelType.Int)],
        static (_, values) => values.Length == 0 ? CelValue.FromInt64(0) : values[0]);

    internal static CelObjectType KnownValue() => new KnownType(
        "google.protobuf.Value",
        typeof(ValueMarker),
        [new(0, "bool_value", CelType.Bool), new(1, "string_value", CelType.String)],
        static (_, values) => values.Length == 0 ? CelValue.Null : values[^1]);

    internal static CelObjectType KnownAny() => new KnownType(
        "google.protobuf.Any",
        typeof(AnyMarker),
        [new(0, "type_url", CelType.String), new(1, "value", CelType.Bytes)],
        static (_, values) => values.Length == 0 ? CelValue.Null : values[0]);

    // Compiles the expression, checked, and evaluates it on the bindings; the cost of the evaluation with a limit that is never
    // reached is in the second result.
    internal static (CelValue Value, ulong Cost) Run(CelEnvironment environment, string expression, Dictionary<string, CelValue>? bindings = null)
    {
        Plan plan = environment.Compile<CelValue>(expression).GetProgramOrThrow().Plan;
        Frame frame = plan.CreateFrame(bindings ?? new Dictionary<string, CelValue>(), ulong.MaxValue, CancellationToken.None);
        CelValue value = plan.Run(frame);
        return (value, frame.ChargedUnits);
    }

    // The same with a limit of cost.
    internal static CelValue RunWithLimit(CelEnvironment environment, string expression, ulong limit)
    {
        Plan plan = environment.Compile<CelValue>(expression).GetProgramOrThrow().Plan;
        return plan.Run(plan.CreateFrame(new Dictionary<string, CelValue>(), limit, CancellationToken.None));
    }

    // The error of a value that has to be one.
    internal static CelError ErrorOf(CelValue value) =>
        value.TryGetError(out CelError? error) ? error : throw new InvalidOperationException("The value is not an error value: " + value.Kind);

    internal static class Int32Marker;

    internal static class ValueMarker;

    internal static class AnyMarker;

    // The instance of acme.test.Message: the value of each field that a literal set.
    internal sealed class Message
    {
        internal CelValue?[] Values { get; } = new CelValue?[MessageFields.Length];
    }

    // The description of acme.test.Message. A literal that sets small to a number outside 0 to 100 is ConversionRange, as a field
    // of 8 bits would say; the creator, when there is one, makes the value of the literal instead.
    internal sealed class MessageType(
        string name,
        Type clrType,
        CelObjectField[] fields,
        Func<MessageType, int[], CelValue[], CelValue>? creator,
        Func<int, long>? readCost)
        : CelObjectType(name, clrType, fields)
    {
        // The calls of CreateInstance: the numbers and the values it got.
        internal List<(int[] Numbers, CelValue[] Values)> Calls { get; } = [];

        // How many times evaluation asked for the extra cost of reading a field.
        internal int ReadCostCalls { get; private set; }

        protected internal override bool IsConstructible => true;

        protected internal override long FieldReadCost(object instance, int number)
        {
            ReadCostCalls++;
            return readCost?.Invoke(number) ?? 0;
        }

        protected internal override CelValue GetField(object instance, int number) =>
            ((Message)instance).Values[number] ?? number switch
            {
                Id or Small or Extension => CelValue.FromInt64(0),
                Label => CelValue.FromString(string.Empty),
                Ratio => CelValue.FromDouble(0),
                Blob => CelValue.FromBytes(Array.Empty<byte>()),
                Tags => CelValue.FromList(CelList.Create()),
                Attributes => CelValue.FromMap(CelMap.Create([])),
                _ => CelValue.Null,
            };

        protected internal override bool HasField(object instance, int number) => ((Message)instance).Values[number] is not null;

        protected internal override string? GetExcludedFieldReason(string fieldName) =>
            fieldName == "hidden" ? "the member is not public" : null;

        protected internal override CelValue CreateInstance(ReadOnlySpan<int> fieldNumbers, ReadOnlySpan<CelValue> fieldValues)
        {
            int[] numbers = fieldNumbers.ToArray();
            CelValue[] values = fieldValues.ToArray();
            Calls.Add((numbers, values));
            if (creator is not null)
            {
                return creator(this, numbers, values);
            }

            var message = new Message();
            for (int i = 0; i < numbers.Length; i++)
            {
                if (numbers[i] == Small && values[i].TryGetInt64(out long small) && small is < 0 or > 100)
                {
                    return FieldError(Small, CelErrorCode.ConversionRange, small + " is out of the range of small");
                }

                message.Values[numbers[i]] = values[i];
            }

            return FromInstance(message);
        }

        // The error value of a field that cannot be set, for a creator.
        internal CelValue Failure(int number, CelErrorCode code, string detail) => FieldError(number, code, detail);

        // A bare object value of an instance, for a creator.
        internal static CelValue Wrap(object instance) => FromInstance(instance);
    }

    // The description of a known type: a literal of it is the value that the maker returns for its values; no object is an
    // instance of its marker class.
    private sealed class KnownType(
        string name,
        Type marker,
        CelObjectField[] fields,
        Func<int[], CelValue[], CelValue> maker) : CelObjectType(name, marker, fields)
    {
        protected internal override bool IsConstructible => true;

        protected internal override CelValue GetField(object instance, int number) => CelValue.Null;

        protected internal override bool HasField(object instance, int number) => false;

        protected internal override CelValue CreateInstance(ReadOnlySpan<int> fieldNumbers, ReadOnlySpan<CelValue> fieldValues) =>
            maker(fieldNumbers.ToArray(), fieldValues.ToArray());
    }
}

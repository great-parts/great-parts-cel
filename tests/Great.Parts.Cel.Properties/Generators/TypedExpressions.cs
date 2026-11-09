using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CsCheck;
using Great.Parts.Cel.Conformance.Runner;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Properties.Generators;

/// <summary>A generated expression with its target type and the bindings it is evaluated on.</summary>
/// <param name="Text">The text of the expression.</param>
/// <param name="Target">The CEL type the expression was built for.</param>
/// <param name="Bindings">The values of the variables, of their declared types; some variables are unbound.</param>
/// <param name="BindingsText">The bindings as text, for failure messages.</param>
internal sealed record TypedCase(string Text, CelType Target, CelBindings Bindings, string BindingsText)
{
    public override string ToString() => $"{Text} on {BindingsText}";
}

/// <summary>
/// Generates well-typed expressions of the core (docs/design/tests.md, "Property tests and the seed"): for a target CEL type an
/// expression over the standard library version 1 — literals, the variables of a fixed environment, calls, <c>&amp;&amp;</c>,
/// <c>||</c>, <c>?:</c>, indexes, selections and <c>has()</c> on maps, the standard macros — within depth 8 and 60 nodes, and
/// bindings of the declared types: a <c>dyn</c> variable bound to any kind, one variable in ten unbound. With the part of time,
/// the environment has the variables <c>ts</c> and <c>du</c> and the time zone database of the whole test archive, and
/// <c>timestamp</c> and <c>duration</c> are target types.
/// </summary>
internal static class TypedExpressions
{
    internal const int MaxDepth = 8;
    internal const int MaxNodes = 60;

    // The most children a compound adds at once: a compound starts only while they fit within MaxNodes.
    private const int LargestCompound = 4;

    internal static readonly CelType ListInt = CelType.ListOf(CelType.Int);
    internal static readonly CelType ListDyn = CelType.ListOf(CelType.Dyn);
    internal static readonly CelType MapStringInt = CelType.MapOf(CelType.String, CelType.Int);
    internal static readonly CelType MapIntDyn = CelType.MapOf(CelType.Int, CelType.Dyn);
    internal static readonly CelType TypeType = CelType.TypeOf(CelType.Dyn);

    /// <summary>
    /// The variables of the fixed environment: one of each type, two of type <c>dyn</c>, and the variables of application data —
    /// <c>o</c> of <c>gen.Item</c>, <c>g</c> of <c>gen.Bag</c> and <c>j</c> of type <c>dyn</c>, bound to a <see cref="System.Text.Json.JsonElement"/> —
    /// and <c>ts</c> of type <c>timestamp</c> and <c>du</c> of type <c>duration</c>.
    /// </summary>
    internal static readonly (string Name, CelType Type)[] Variables =
    [
        ("i", CelType.Int), ("u", CelType.UInt), ("d", CelType.Double), ("b", CelType.Bool), ("s", CelType.String),
        ("y", CelType.Bytes), ("li", ListInt), ("ld", ListDyn), ("msi", MapStringInt), ("mid", MapIntDyn), ("t", TypeType),
        ("x1", CelType.Dyn), ("x2", CelType.Dyn),
        ("o", GeneratedTypes.ItemName), ("g", GeneratedTypes.BagName), ("j", CelType.Dyn),
        ("ts", CelType.Timestamp), ("du", CelType.Duration),
    ];

    /// <summary>The target types of the generator.</summary>
    internal static readonly CelType[] Targets =
    [
        CelType.Int, CelType.UInt, CelType.Double, CelType.Bool, CelType.String, CelType.Bytes, ListInt, ListDyn, MapStringInt,
        MapIntDyn, TypeType, CelType.Timestamp, CelType.Duration, CelType.Dyn,
    ];

    /// <summary>The environment of <see cref="CelStandardLibrary.V1"/> with the variables of the generator.</summary>
    internal static readonly CelEnvironment Environment = Declare(new CelEnvironmentBuilder(CelStandardLibrary.V1)).Build().GetEnvironmentOrThrow();

    /// <summary>A well-typed expression of a random target type with its bindings.</summary>
    internal static readonly Gen<TypedCase> WellTyped = Gen.Select(Gen.UInt, Gen.ULong, (stream, seed) => Create(new PCG(stream, seed), null, damaged: false));

    /// <summary>An expression of the generator with one node replaced by a node of another type.</summary>
    internal static readonly Gen<TypedCase> Damaged = Gen.Select(Gen.UInt, Gen.ULong, (stream, seed) => Create(new PCG(stream, seed), null, damaged: true));

    /// <summary>A well-typed expression of the given target type with its bindings.</summary>
    /// <param name="target">One of <see cref="Targets"/>.</param>
    /// <returns>The generator.</returns>
    internal static Gen<TypedCase> Of(CelType target) =>
        Gen.Select(Gen.UInt, Gen.ULong, (stream, seed) => Create(new PCG(stream, seed), target, damaged: false));

    /// <summary>Declares the type source, the time zone database and the variables of the generator.</summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder.</returns>
    internal static CelEnvironmentBuilder Declare(CelEnvironmentBuilder builder)
    {
        builder.AddTypes(GeneratedTypes.Provider);
        builder.UseTimeZones(ZoneInfoFile.Database());
        foreach ((string name, CelType type) in Variables)
        {
            builder.AddVariable(name, type);
        }

        return builder;
    }

    private static TypedCase Create(PCG random, CelType? target, bool damaged)
    {
        CelType type = target ?? Targets[random.Next((uint)Targets.Length)];
        var writer = new Writer(random, damaged ? (int)random.Next(MaxNodes / 2) : -1);
        string text = writer.Expression(type, 0);
        (CelBindings bindings, string bindingsText) = Bind(random);
        return new TypedCase(text, type, bindings, bindingsText);
    }

    /// <summary>Bindings of the declared types, one variable in ten unbound, a <c>dyn</c> variable bound to any kind.</summary>
    /// <param name="random">The source of randomness.</param>
    /// <returns>The bindings and their text.</returns>
    internal static (CelBindings Bindings, string Text) Bind(PCG random)
    {
        var bindings = new CelBindings();
        var text = new StringBuilder("{");
        foreach ((string name, CelType type) in Variables)
        {
            if (random.Next(10) == 0)
            {
                continue;
            }

            if (text.Length > 1)
            {
                text.Append(", ");
            }

            text.Append(name).Append(": ");
            switch (name)
            {
                case "o":
                    text.Append(GeneratedTypes.BindItem(random, bindings));
                    break;
                case "g":
                    text.Append(GeneratedTypes.BindBag(random, bindings));
                    break;
                case "j":
                    text.Append(GeneratedTypes.BindJson(random, bindings));
                    break;
                default:
                    CelValue value = Value(random, type, 0);
                    bindings.Add(name, value);
                    Print(text, value);
                    break;
            }
        }

        return (bindings, text.Append('}').ToString());
    }

    /// <summary>Writes a value as a CEL literal, for failure messages.</summary>
    /// <param name="text">The text to append to.</param>
    /// <param name="value">The value.</param>
    internal static void Print(StringBuilder text, CelValue value)
    {
        switch (value.Kind)
        {
            case CelValueKind.Bool:
                _ = value.TryGetBoolean(out bool boolean);
                text.Append(boolean ? "true" : "false");
                break;
            case CelValueKind.Int:
                _ = value.TryGetInt64(out long integer);
                text.Append(integer.ToString(CultureInfo.InvariantCulture));
                break;
            case CelValueKind.UInt:
                _ = value.TryGetUInt64(out ulong unsigned);
                text.Append(unsigned.ToString(CultureInfo.InvariantCulture)).Append('u');
                break;
            case CelValueKind.Double:
                _ = value.TryGetDouble(out double number);
                text.Append(number.ToString("R", CultureInfo.InvariantCulture));
                break;
            case CelValueKind.String:
                _ = value.TryGetString(out string? str);
                text.Append('"').Append(str).Append('"');
                break;
            case CelValueKind.Bytes:
                _ = value.TryGetBytes(out ReadOnlyMemory<byte> bytes);
                text.Append("b\"");
                foreach (byte unit in bytes.Span)
                {
                    text.Append(CultureInfo.InvariantCulture, $"\\x{unit:x2}");
                }

                text.Append('"');
                break;
            case CelValueKind.List or CelValueKind.Map:
                PrintCollection(text, value);
                break;
            case CelValueKind.Timestamp:
                _ = value.TryGetTimestamp(out CelTimestamp moment);
                text.Append("timestamp('").Append(moment).Append("')");
                break;
            case CelValueKind.Duration:
                _ = value.TryGetDuration(out CelDuration span);
                text.Append("duration('").Append(span).Append("')");
                break;
            case CelValueKind.Type:
                _ = value.TryGetType(out CelType? type);
                text.Append(type!.Name);
                break;
            case CelValueKind.Object:
                _ = value.TryGetObject(out object? instance);
                text.Append("object ").Append(instance);
                break;
            default:
                text.Append(value.Kind.ToString());
                break;
        }
    }

    private static void PrintCollection(StringBuilder text, CelValue value)
    {
        if (value.TryGetList(out CelList? list))
        {
            text.Append('[');
            for (int index = 0; index < list.Count; index++)
            {
                text.Append(index == 0 ? "" : ", ");
                Print(text, list[index]);
            }

            text.Append(']');
            return;
        }

        _ = value.TryGetMap(out CelMap? map);
        text.Append('{');
        bool first = true;
        foreach (KeyValuePair<CelValue, CelValue> entry in map!)
        {
            text.Append(first ? "" : ", ");
            first = false;
            Print(text, entry.Key);
            text.Append(": ");
            Print(text, entry.Value);
        }

        text.Append('}');
    }

    private static readonly long[] Integers = [0, 1, 2, -1, 7, 42, -100, long.MaxValue, long.MinValue];
    private static readonly ulong[] Unsigned = [0, 1, 3, 10, ulong.MaxValue];
    private static readonly double[] Doubles = [0.0, -0.0, 1.5, -2.5, 0.1, 1e300, double.NaN, double.PositiveInfinity, 3.0];
    private static readonly string[] Strings = ["", "a", "ab", "B", "İı", "1.5", "42", "true", "-7", "é😀"];
    private static readonly CelType[] TypeValues = [CelType.Int, CelType.String, ListDyn, CelType.Bool, CelType.Null, TypeType];

    // A value of the declared type; a dyn value of any kind, a collection at the top level only.
    private static CelValue Value(PCG random, CelType type, int depth)
    {
        if (type.Equals(CelType.Dyn))
        {
            type = depth == 0 ? Targets[random.Next((uint)Targets.Length - 1)] : Targets[random.Next(6)];
            if (random.Next(12) == 0)
            {
                return CelValue.Null;
            }
        }

        if (type.Equals(CelType.Int))
        {
            return CelValue.FromInt64(Integers[random.Next((uint)Integers.Length)]);
        }

        if (type.Equals(CelType.UInt))
        {
            return CelValue.FromUInt64(Unsigned[random.Next((uint)Unsigned.Length)]);
        }

        if (type.Equals(CelType.Double))
        {
            return CelValue.FromDouble(Doubles[random.Next((uint)Doubles.Length)]);
        }

        if (type.Equals(CelType.Bool))
        {
            return CelValue.FromBoolean(random.Next(2) == 0);
        }

        if (type.Equals(CelType.String))
        {
            return CelValue.FromString(Strings[random.Next((uint)Strings.Length)]);
        }

        if (type.Equals(CelType.Bytes))
        {
            byte[] bytes = new byte[random.Next(4)];
            for (int index = 0; index < bytes.Length; index++)
            {
                bytes[index] = (byte)random.Next(256);
            }

            return CelValue.FromBytes(bytes);
        }

        if (type.Equals(CelType.Timestamp))
        {
            return TimeText.BoundTimestamp(random);
        }

        if (type.Equals(CelType.Duration))
        {
            return TimeText.BoundDuration(random);
        }

        return type.Kind == CelTypeKind.Type
            ? CelValue.FromType(TypeValues[random.Next((uint)TypeValues.Length)])
            : Collection(random, type, depth);
    }

    // A list or a map of up to four elements of the parameter types; map keys are distinct.
    private static CelValue Collection(PCG random, CelType type, int depth)
    {
        int count = (int)random.Next(5);
        if (type.Kind == CelTypeKind.List)
        {
            var items = new CelValue[count];
            for (int index = 0; index < count; index++)
            {
                items[index] = Value(random, type.Parameters[0], depth + 1);
            }

            return CelValue.FromList(CelList.Create(items));
        }

        // Maps of the generator: map(string, int) and map(int, dyn), with distinct keys.
        var entries = new List<KeyValuePair<CelValue, CelValue>>(count);
        for (int index = 0; index < count; index++)
        {
            CelValue key = type.Parameters[0].Equals(CelType.String)
                ? CelValue.FromString(Strings[index])
                : CelValue.FromInt64(index - 1);
            entries.Add(new(key, Value(random, type.Parameters[1], depth + 1)));
        }

        return CelValue.FromMap(CelMap.Create(entries));
    }

    // Writes one expression: each call of Expression is one node.
    private sealed class Writer(PCG random, int damagedNode)
    {
        private static readonly string[] IntLiterals = ["0", "1", "2", "-1", "7", "42", "9223372036854775807", "-9223372036854775808"];
        private static readonly string[] UIntLiterals = ["0u", "1u", "3u", "18446744073709551615u"];
        private static readonly string[] DoubleLiterals = ["0.0", "1.5", "-2.5", "0.1", "1e300", "-0.0"];
        private static readonly string[] StringLiterals = ["''", "'a'", "'ab'", "'B'", "'İı'", "'1.5'", "'42'", "'true'", "'é😀'"];
        private static readonly string[] BytesLiterals = ["b''", "b'ab'", "b'\\xff'", "b'a\\x00'"];
        private static readonly string[] TypeNames = ["int", "uint", "double", "bool", "string", "bytes", "list", "map", "type", "null_type"];
        private static readonly string[] Fields = ["a", "b", "f"];
        private static readonly string[] Ordered = ["<", "<=", ">", ">="];
        private static readonly string[] Relations = ["<", "<=", ">", ">=", "==", "!="];
        private static readonly string[] TimestampReceivers =
            ["getFullYear", "getMonth", "getDayOfYear", "getDayOfMonth", "getDate", "getDayOfWeek", "getHours", "getMinutes", "getSeconds", "getMilliseconds"];
        private static readonly string[] DurationReceivers = ["getHours", "getMinutes", "getSeconds", "getMilliseconds"];

        private readonly List<(string Name, CelType Type)> _scope = [];
        private int _nodes;
        private int _locals;

        internal string Expression(CelType type, int depth)
        {
            int node = _nodes++;
            if (node == damagedNode)
            {
                // A node of another type than the position takes, never dyn.
                CelType other;
                do
                {
                    other = Targets[random.Next((uint)Targets.Length - 1)];
                }
                while (other.Equals(type));
                type = other;
            }

            bool leaf = depth >= MaxDepth - 1 || _nodes + LargestCompound > MaxNodes || random.Next(4) == 0;
            return leaf ? Leaf(type) : Compound(type, depth + 1);
        }

        private string Any(int depth) => Expression(Targets[random.Next((uint)Targets.Length)], depth);

        private string Pick(params ReadOnlySpan<string> choices) => choices[(int)random.Next((uint)choices.Length)];

        private string Leaf(CelType type)
        {
            // A variable of the scope or of the environment with the type, or a dyn variable now and then.
            if (random.Next(3) == 0)
            {
                var names = new List<string>();
                foreach ((string name, CelType scoped) in _scope)
                {
                    if (scoped.Equals(type))
                    {
                        names.Add(name);
                    }
                }

                foreach ((string name, CelType declared) in Variables)
                {
                    if (declared.Equals(type))
                    {
                        names.Add(name);
                    }
                }

                if (names.Count > 0)
                {
                    return names[(int)random.Next((uint)names.Count)];
                }
            }

            if (random.Next(16) == 0)
            {
                return Pick("x1", "x2");
            }

            return Literal(type);
        }

        // Lists and maps are written non-empty: the elements of [] and {} have a free type, which the first overload of a call
        // binds, as cel-go does, so that -([][0]) is a double. Empty values come from the bindings and from filter.
        private string Literal(CelType type)
        {
            if (type.Equals(CelType.Int))
            {
                return Pick(IntLiterals);
            }

            if (type.Equals(CelType.UInt))
            {
                return Pick(UIntLiterals);
            }

            if (type.Equals(CelType.Double))
            {
                return Pick(DoubleLiterals);
            }

            if (type.Equals(CelType.Bool))
            {
                return Pick("true", "false");
            }

            if (type.Equals(CelType.String))
            {
                return Pick(StringLiterals);
            }

            if (type.Equals(CelType.Bytes))
            {
                return Pick(BytesLiterals);
            }

            if (type.Equals(ListInt))
            {
                return Pick("[1, 2]", "[0]", "[-1, 7, 42]");
            }

            if (type.Equals(ListDyn))
            {
                return Pick("[1, 'a']", "[1u, 1.0, true]", "[[1], {}]");
            }

            if (type.Equals(MapStringInt))
            {
                return Pick("{'a': 1}", "{'a': 1, 'b': 2}");
            }

            if (type.Equals(MapIntDyn))
            {
                return Pick("{1: dyn('a')}", "{0: 1, 1: 'b'}", "{-1: dyn([1])}");
            }

            if (type.Kind == CelTypeKind.Type)
            {
                return Pick(TypeNames);
            }

            if (type.Kind is CelTypeKind.Timestamp or CelTypeKind.Duration)
            {
                return TimeLiteral(type);
            }

            // dyn: null, a dyn variable, or a literal of another type under dyn(), so that the static type is dyn.
            return random.Next(4) switch
            {
                0 => "dyn(null)",
                1 => Pick("x1", "x2"),
                _ => $"dyn({Literal(Targets[random.Next((uint)Targets.Length - 1)])})",
            };
        }

        // A timestamp from a string of the grammar of time, valid or mutated, or from an int near the limits of the range; a duration
        // from a string.
        private string TimeLiteral(CelType type)
        {
            if (type.Kind == CelTypeKind.Duration)
            {
                return $"duration('{TimeText.Duration(random)}')";
            }

            return random.Next(5) == 0
                ? $"timestamp({TimeText.TimestampInteger(random).ToString(CultureInfo.InvariantCulture)})"
                : $"timestamp('{TimeText.Timestamp(random)}')";
        }

        private string Compound(CelType type, int depth)
        {
            if (random.Next(10) == 0)
            {
                return $"({Expression(CelType.Bool, depth)} ? {Expression(type, depth)} : {Expression(type, depth)})";
            }

            if (random.Next(6) == 0 && ApplicationData(type, depth) is { } data)
            {
                return data;
            }

            return type.Kind switch
            {
                CelTypeKind.Timestamp => TimestampCompound(depth),
                CelTypeKind.Duration => DurationCompound(depth),
                _ => Typed(type, depth),
            };
        }

        // The conversions, the arithmetic and the identity of timestamps; the receivers and the relations are in the int and bool compounds.
        private string TimestampCompound(int depth) => random.Next(6) switch
        {
            0 => $"timestamp({Expression(CelType.Int, depth)})",
            1 => $"timestamp({Expression(CelType.Timestamp, depth)})",
            2 => $"timestamp(string({Expression(CelType.Timestamp, depth)}))",
            3 => $"({Expression(CelType.Timestamp, depth)} + {Expression(CelType.Duration, depth)})",
            4 => $"({Expression(CelType.Duration, depth)} + {Expression(CelType.Timestamp, depth)})",
            _ => $"({Expression(CelType.Timestamp, depth)} - {Expression(CelType.Duration, depth)})",
        };

        private string DurationCompound(int depth) => random.Next(5) switch
        {
            0 => $"duration({Expression(CelType.Duration, depth)})",
            1 => $"duration(string({Expression(CelType.Duration, depth)}))",
            2 => $"({Expression(CelType.Duration, depth)} + {Expression(CelType.Duration, depth)})",
            3 => $"({Expression(CelType.Duration, depth)} - {Expression(CelType.Duration, depth)})",
            _ => $"({Expression(CelType.Timestamp, depth)} - {Expression(CelType.Timestamp, depth)})",
        };

        private CelType TimeType() => random.Next(2) == 0 ? CelType.Timestamp : CelType.Duration;

        // One of the ten receivers of a timestamp, without a zone or with a zone of the pool, or one of the four receivers of a duration.
        private string Receiver(int depth)
        {
            if (random.Next(3) == 0)
            {
                return $"{Expression(CelType.Duration, depth)}.{Pick(DurationReceivers)}()";
            }

            string zone = random.Next(2) == 0 ? "" : $"'{TimeText.Zone(random)}'";
            return $"{Expression(CelType.Timestamp, depth)}.{Pick(TimestampReceivers)}({zone})";
        }

        private string Typed(CelType type, int depth)
        {
            if (type.Equals(CelType.Int))
            {
                return IntCompound(depth);
            }

            if (type.Equals(CelType.UInt))
            {
                return random.Next(3) switch
                {
                    0 => $"uint({Expression(Pick3(CelType.Int, CelType.Double, CelType.String), depth)})",
                    _ => $"({Expression(CelType.UInt, depth)} {Pick("+", "-", "*", "/", "%")} {Expression(CelType.UInt, depth)})",
                };
            }

            if (type.Equals(CelType.Double))
            {
                return random.Next(4) switch
                {
                    0 => $"double({Expression(Pick3(CelType.Int, CelType.UInt, CelType.String), depth)})",
                    1 => $"-(double({Expression(CelType.Double, depth)}))",
                    _ => $"({Expression(CelType.Double, depth)} {Pick("+", "-", "*", "/")} {Expression(CelType.Double, depth)})",
                };
            }

            if (type.Equals(CelType.Bool))
            {
                return BoolCompound(depth);
            }

            if (type.Equals(CelType.String))
            {
                return random.Next(4) switch
                {
                    0 => $"string({Expression(Targets[random.Next(6)], depth)})",
                    1 => $"string({Expression(TimeType(), depth)})",
                    _ => $"({Expression(CelType.String, depth)} + {Expression(CelType.String, depth)})",
                };
            }

            if (type.Equals(CelType.Bytes))
            {
                return random.Next(3) switch
                {
                    0 => $"bytes({Expression(CelType.String, depth)})",
                    _ => $"({Expression(CelType.Bytes, depth)} + {Expression(CelType.Bytes, depth)})",
                };
            }

            if (type.Equals(ListInt))
            {
                return random.Next(4) switch
                {
                    0 => $"[{Expression(CelType.Int, depth)}, {Expression(CelType.Int, depth)}]",
                    1 => $"({Expression(ListInt, depth)} + {Expression(ListInt, depth)})",
                    2 => Macro(ListInt, depth, "map", CelType.Int),
                    _ => Macro(ListInt, depth, "filter", CelType.Bool),
                };
            }

            if (type.Equals(ListDyn))
            {
                return random.Next(4) switch
                {
                    0 => $"[dyn({Any(depth)}), {Any(depth)}]",
                    1 => $"({Expression(ListDyn, depth)} + {Expression(ListDyn, depth)})",
                    2 => Macro(ListDyn, depth, "filter", CelType.Bool),
                    _ => Macro(Range(), depth, "map", CelType.Dyn),
                };
            }

            if (type.Equals(MapStringInt))
            {
                return $"{{'{Pick(Fields)}': {Expression(CelType.Int, depth)}, 'k': {Expression(CelType.Int, depth)}}}";
            }

            if (type.Equals(MapIntDyn))
            {
                return $"{{{random.Next(5)}: dyn({Any(depth)}), 10: {Any(depth)}}}";
            }

            if (type.Kind == CelTypeKind.Type)
            {
                return $"type({Any(depth)})";
            }

            return DynCompound(depth);
        }

        // An expression of application data: the types and carriers of the generator, a JsonElement. Selections go two levels deep
        // at most; the fields of a type described at run time are absent, null or of another type in some bindings. A type
        // without such an expression gets none.
        private string? ApplicationData(CelType type, int depth)
        {
            if (type.Equals(CelType.Int))
            {
                return random.Next(4) switch
                {
                    0 => Pick("o.n", "o.c.n", "g.n", "g.c.n"),
                    1 => Pick("size(o.l)", "size(o.c.l)", "size(j)", "size(j.c)", "size(j.e)", "size(j.b)"),
                    2 => $"size(j.c[{Expression(CelType.Int, depth)}])",
                    _ => Pick("int(j.a)", "int(j.c[0])", "int(j.n)"),
                };
            }

            if (type.Equals(CelType.Bool))
            {
                return random.Next(8) switch
                {
                    0 => Pick("has(o.n)", "has(o.w)", "has(o.c)", "has(o.c.w)", "has(o.c.c)", "has(o.c.l)"),
                    1 => Pick("has(g.n)", "has(g.s)", "has(g.c)", "has(g.c.n)", "has(g.c.c)"),
                    2 => Pick("(o == o.c)", "(o != o.c)", "(o.c == o.c.c)", "(o == o)", "(o.c == null)", "(o.c.c != null)"),
                    3 => Pick("(g == g.c)", "(g != g.c)", "(g.c == g.c.c)", "(g == g)", "(g.c == null)"),
                    4 => Pick("(type(o) == gen.Item)", "(type(g) == gen.Bag)", "(type(o.c) == gen.Item)", "(type(g.c) != gen.Bag)", "(type(j) == map)"),
                    5 => Pick("has(j.a)", "has(j.e.f)", "has(j.e.h)", "has(j.zz)", "has(j.e.f.g)"),
                    6 => $"({Expression(CelType.String, depth)} in j)",
                    _ => $"({Expression(CelType.Int, depth)} in j.c)",
                };
            }

            if (type.Equals(CelType.String))
            {
                return Pick("o.s", "o.c.s", "g.s", "g.c.s", "string(j.b)", "string(j.c[1])");
            }

            if (type.Equals(ListInt))
            {
                return Pick("o.l", "o.c.l");
            }

            if (type.Kind == CelTypeKind.Type)
            {
                return Pick("type(o)", "type(g)", "type(o.c)", "type(g.c)", "type(j)", "type(j.a)", "type(j.c)");
            }

            if (type.Equals(CelType.Dyn))
            {
                return random.Next(4) switch
                {
                    0 => Pick("j", "j.a", "j.c", "j.e", "j.e.f", "j.e.h", "j.zz"),
                    1 => $"j.c[{Expression(CelType.Int, depth)}]",
                    2 => Pick("dyn(o)", "dyn(o.w)", "dyn(o.c)", "dyn(g)", "dyn(g.c)", "dyn(o.l)"),
                    _ => $"j[{Expression(CelType.String, depth)}]",
                };
            }

            return null;
        }

        private string IntCompound(int depth) => random.Next(12) switch
        {
            // The operand of a negation goes through int(): an index of [], {} or a dyn value has a free type, which -_ binds to
            // double, its first overload, as cel-go does.
            0 => $"-(int({Expression(CelType.Int, depth)}))",
            1 => $"size({Expression(Sized(), depth)})",
            2 => $"{Expression(Sized(), depth)}.size()",
            3 => $"int({Expression(Pick3(CelType.UInt, CelType.Double, CelType.String), depth)})",
            4 => $"{Expression(ListInt, depth)}[{Expression(CelType.Int, depth)}]",
            5 => $"{Expression(MapStringInt, depth)}[{Expression(CelType.String, depth)}]",
            6 => $"{Expression(MapStringInt, depth)}.{Pick(Fields)}",
            7 => $"int({Expression(TimeType(), depth)})",
            8 or 9 => Receiver(depth),
            _ => $"({Expression(CelType.Int, depth)} {Pick("+", "-", "*", "/", "%")} {Expression(CelType.Int, depth)})",
        };

        private string BoolCompound(int depth)
        {
            switch (random.Next(12))
            {
                case 0:
                    return $"!({Expression(CelType.Bool, depth)})";
                case 1:
                    return $"({Expression(CelType.Bool, depth)} {Pick("&&", "||")} {Expression(CelType.Bool, depth)})";
                case 2:
                    {
                        CelType compared = Targets[random.Next((uint)Targets.Length)];
                        return $"({Expression(compared, depth)} {Pick("==", "!=")} {Expression(compared, depth)})";
                    }

                case 3:
                    {
                        CelType compared = Targets[random.Next(6)];
                        return $"({Expression(compared, depth)} {Pick(Ordered)} {Expression(compared, depth)})";
                    }

                case 4:
                    return $"({Expression(CelType.Int, depth)} in {Expression(ListInt, depth)})";
                case 8:
                    {
                        CelType time = TimeType();
                        return $"({Expression(time, depth)} {Pick(Relations)} {Expression(time, depth)})";
                    }

                case 5:
                    return $"({Expression(CelType.String, depth)} in {Expression(MapStringInt, depth)})";
                case 6:
                    return $"has({Pick("msi", "mid", "x1")}.{Pick(Fields)})";
                case 7:
                    return $"{Expression(CelType.String, depth)}.{Pick("contains", "startsWith", "endsWith")}({Expression(CelType.String, depth)})";
                default:
                    return Macro(Range(), depth, Pick("all", "exists", "exists_one"), CelType.Bool);
            }
        }

        private string DynCompound(int depth) => random.Next(6) switch
        {
            0 => $"dyn({Any(depth)})",
            1 => $"{Expression(ListDyn, depth)}[{Expression(CelType.Int, depth)}]",
            2 => $"{Expression(MapIntDyn, depth)}[{Expression(CelType.Int, depth)}]",
            3 => $"{Pick("x1", "x2")}.{Pick(Fields)}",

            // Keys of the four key types, pairwise unequal: 1 and 2u differ under the equality of numbers.
            4 => $"{{1: dyn({Any(depth)}), 'a': {Any(depth)}, true: {Any(depth)}, 2u: {Any(depth)}}}",
            _ => $"dyn({Expression(Targets[random.Next((uint)Targets.Length - 1)], depth)})",
        };

        // A macro over a range of the given type, with a fresh variable of the element or key type in the scope of its body.
        private string Macro(CelType range, int depth, string macro, CelType bodyType)
        {
            string target = Expression(range, depth);
            string variable = "v" + _locals++.ToString(CultureInfo.InvariantCulture);
            // The element type of a list, the key type of a map.
            _scope.Add((variable, range.Parameters[0]));
            string body = Expression(bodyType, depth);
            _scope.RemoveAt(_scope.Count - 1);
            return $"{target}.{macro}({variable}, {body})";
        }

        // The ranges of macros: the two lists and the two maps.
        private CelType Range() => Pick4(ListInt, ListDyn, MapStringInt, MapIntDyn);

        // The arguments of size: strings, bytes, lists and maps.
        private CelType Sized() => random.Next(6) switch
        {
            0 => CelType.String,
            1 => CelType.Bytes,
            _ => Range(),
        };

        private CelType Pick3(CelType first, CelType second, CelType third) => random.Next(3) switch
        {
            0 => first,
            1 => second,
            _ => third,
        };

        private CelType Pick4(CelType first, CelType second, CelType third, CelType fourth) => random.Next(4) switch
        {
            0 => first,
            1 => second,
            2 => third,
            _ => fourth,
        };
    }
}

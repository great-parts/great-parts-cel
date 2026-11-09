using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Library.Time;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using static Great.Parts.Cel.Library.Comparisons;

namespace Great.Parts.Cel.Library;

// The table of the standard library, version 1. It is built once per process and never changes: a later
// version is a new table, and a fix of behavior changes an implementation that all versions share.
internal static class StandardLibraryV1
{
    internal const string Source = "standard library v1";

    private static readonly CelType A = CelType.TypeParameter("A");
    private static readonly CelType B = CelType.TypeParameter("B");
    private static readonly CelType ListOfA = CelType.ListOf(A);
    private static readonly CelType MapOfAB = CelType.MapOf(A, B);

    // The kinds that ordering takes, each with its type and the suffix of its overload identifiers. The
    // order is that of cel-go: for each relation bool, then each number with itself and the other two
    // numbers, then string and bytes.
    private static readonly (CelType Left, CelType Right)[] OrderedPairs =
    [
        (CelType.Bool, CelType.Bool),
        (CelType.Int, CelType.Int),
        (CelType.Int, CelType.Double),
        (CelType.Int, CelType.UInt),
        (CelType.UInt, CelType.UInt),
        (CelType.UInt, CelType.Double),
        (CelType.UInt, CelType.Int),
        (CelType.Double, CelType.Double),
        (CelType.Double, CelType.Int),
        (CelType.Double, CelType.UInt),
        (CelType.String, CelType.String),
        (CelType.Bytes, CelType.Bytes),
    ];

    // The type identifiers of version 1 with the types they stand for: their values are the type values of
    // these types, with the parameters of list, map and type erased.
    internal static FrozenDictionary<string, CelType> TypeIdentifiers { get; } = new Dictionary<string, CelType>(StringComparer.Ordinal)
    {
        ["bool"] = CelType.Bool,
        ["bytes"] = CelType.Bytes,
        ["double"] = CelType.Double,
        ["int"] = CelType.Int,
        ["uint"] = CelType.UInt,
        ["string"] = CelType.String,
        ["null_type"] = CelType.Null,
        ["type"] = CelType.TypeOf(CelType.Dyn),
        ["list"] = CelType.ListOf(CelType.Dyn),
        ["map"] = CelType.MapOf(CelType.Dyn, CelType.Dyn),
        ["google.protobuf.Timestamp"] = CelType.Timestamp,
        ["google.protobuf.Duration"] = CelType.Duration,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    // The overloads, each function in the order of cel-go. Costs are units of the page Cost,
    // https://github.com/great-parts/great-parts-cel/blob/main/docs/cost.md: fixed units, and a cost function
    // of the arguments where the cost grows with them. Equality charges its pairs itself, and a text result
    // whose length depends on its content charges its part before it is created.
    internal static ImmutableArray<OverloadBinding> Overloads { get; } = Build();

    private static ImmutableArray<OverloadBinding> Build()
    {
        ImmutableArray<OverloadBinding>.Builder table = ImmutableArray.CreateBuilder<OverloadBinding>();
        CelType @bool = CelType.Bool;
        CelType @int = CelType.Int;
        CelType @uint = CelType.UInt;
        CelType @double = CelType.Double;
        CelType @string = CelType.String;
        CelType bytes = CelType.Bytes;

        // Logic: the special forms are nodes of the planner and cost nothing beyond their operands.
        table.Add(Global("_?_:_", OverloadIds.Conditional, [@bool, A, A], A, null, 0));
        table.Add(Global("_&&_", OverloadIds.LogicalAnd, [@bool, @bool], @bool, null, 0));
        table.Add(Global("_||_", OverloadIds.LogicalOr, [@bool, @bool], @bool, null, 0));
        table.Add(Global("!_", OverloadIds.LogicalNot, [@bool], @bool, LogicalNot, 1));
        table.Add(Global("@not_strictly_false", OverloadIds.NotStrictlyFalse, [@bool], @bool, null, 0));

        // Equality charges eq(a, b) pair by pair.
        table.Add(Global("_==_", OverloadIds.EqualsId, [A, A], @bool, AreEqual, 0));
        table.Add(Global("_!=_", OverloadIds.NotEquals, [A, A], @bool, AreNotEqual, 0));

        // Arithmetic.
        table.Add(Global("_+_", OverloadIds.AddBytes, [bytes, bytes], bytes, Strings.AddBytes, 1, Costs.Concatenation));
        table.Add(Global("_+_", OverloadIds.AddDouble, [@double, @double], @double, Arithmetic.AddDouble, 1));
        TimeOverloads.AddAdditions(table, Source);
        table.Add(Global("_+_", OverloadIds.AddInt64, [@int, @int], @int, Arithmetic.AddInt64, 1));
        table.Add(Global("_+_", OverloadIds.AddList, [ListOfA, ListOfA], ListOfA, Collections.AddList, 1, Costs.ListSizes));
        table.Add(Global("_+_", OverloadIds.AddString, [@string, @string], @string, Strings.AddString, 1, Costs.Concatenation));
        table.Add(Global("_+_", OverloadIds.AddUInt64, [@uint, @uint], @uint, Arithmetic.AddUInt64, 1));
        table.Add(Global("_/_", OverloadIds.DivideDouble, [@double, @double], @double, Arithmetic.DivideDouble, 1));
        table.Add(Global("_/_", OverloadIds.DivideInt64, [@int, @int], @int, Arithmetic.DivideInt64, 1));
        table.Add(Global("_/_", OverloadIds.DivideUInt64, [@uint, @uint], @uint, Arithmetic.DivideUInt64, 1));
        table.Add(Global("_%_", OverloadIds.ModuloInt64, [@int, @int], @int, Arithmetic.ModuloInt64, 1));
        table.Add(Global("_%_", OverloadIds.ModuloUInt64, [@uint, @uint], @uint, Arithmetic.ModuloUInt64, 1));
        table.Add(Global("_*_", OverloadIds.MultiplyDouble, [@double, @double], @double, Arithmetic.MultiplyDouble, 1));
        table.Add(Global("_*_", OverloadIds.MultiplyInt64, [@int, @int], @int, Arithmetic.MultiplyInt64, 1));
        table.Add(Global("_*_", OverloadIds.MultiplyUInt64, [@uint, @uint], @uint, Arithmetic.MultiplyUInt64, 1));
        table.Add(Global("-_", OverloadIds.NegateDouble, [@double], @double, Arithmetic.NegateDouble, 1));
        table.Add(Global("-_", OverloadIds.NegateInt64, [@int], @int, Arithmetic.NegateInt64, 1));
        table.Add(Global("_-_", OverloadIds.SubtractDouble, [@double, @double], @double, Arithmetic.SubtractDouble, 1));
        TimeOverloads.AddDurationSubtraction(table, Source);
        table.Add(Global("_-_", OverloadIds.SubtractInt64, [@int, @int], @int, Arithmetic.SubtractInt64, 1));
        TimeOverloads.AddTimestampSubtractions(table, Source);
        table.Add(Global("_-_", OverloadIds.SubtractUInt64, [@uint, @uint], @uint, Arithmetic.SubtractUInt64, 1));

        // Ordering.
        AddRelationWithTime(table, "_<_", OverloadIds.Less, Relation.Less, TimeOverloadIds.LessTimestamp, TimeOverloadIds.LessDuration);
        AddRelationWithTime(table, "_<=_", OverloadIds.LessEquals, Relation.LessEquals, TimeOverloadIds.LessEqualsTimestamp, TimeOverloadIds.LessEqualsDuration);
        AddRelationWithTime(table, "_>_", OverloadIds.Greater, Relation.Greater, TimeOverloadIds.GreaterTimestamp, TimeOverloadIds.GreaterDuration);
        AddRelationWithTime(table, "_>=_", OverloadIds.GreaterEquals, Relation.GreaterEquals, TimeOverloadIds.GreaterEqualsTimestamp, TimeOverloadIds.GreaterEqualsDuration);

        // Index, in and size. A string key costs ⌈L/10⌉ more; in_list charges eq pair by pair.
        table.Add(Global("_[_]", OverloadIds.IndexList, [ListOfA, @int], A, Collections.IndexList, 1, Costs.IndexList));
        table.Add(Global("_[_]", OverloadIds.IndexMap, [MapOfAB, A], B, Collections.IndexMap, 1, Costs.IndexKey));
        table.Add(Global("@in", OverloadIds.InList, [A, ListOfA], @bool, Collections.InList, 1));
        table.Add(Global("@in", OverloadIds.InMap, [A, MapOfAB], @bool, Collections.InMap, 1, Costs.StringKey));
        table.Add(Global("size", OverloadIds.SizeBytes, [bytes], @int, Strings.BytesSize, 1));
        table.Add(Member("size", OverloadIds.BytesSize, [bytes], @int, Strings.BytesSize, 1));
        table.Add(Global("size", OverloadIds.SizeList, [ListOfA], @int, Collections.ListSize, 1));
        table.Add(Member("size", OverloadIds.ListSize, [ListOfA], @int, Collections.ListSize, 1));
        table.Add(Global("size", OverloadIds.SizeMap, [MapOfAB], @int, Collections.MapSize, 1));
        table.Add(Member("size", OverloadIds.MapSize, [MapOfAB], @int, Collections.MapSize, 1));
        table.Add(Global("size", OverloadIds.SizeString, [@string], @int, Strings.StringSize, 1, Costs.Text));
        table.Add(Member("size", OverloadIds.StringSize, [@string], @int, Strings.StringSize, 1, Costs.Text));

        // type, dyn and the conversions.
        table.Add(Global("type", OverloadIds.Type, [A], CelType.TypeOf(A), Conversions.Type, 1));
        table.Add(Global("bool", OverloadIds.BoolToBool, [@bool], @bool, Conversions.BoolIdentity, 1));
        table.Add(Global("bool", OverloadIds.StringToBool, [@string], @bool, Conversions.StringToBool, 1, Costs.Text));
        table.Add(Global("bytes", OverloadIds.BytesToBytes, [bytes], bytes, Conversions.BytesIdentity, 1));
        table.Add(Global("bytes", OverloadIds.StringToBytes, [@string], bytes, Conversions.StringToBytes, 1, Costs.Text));
        table.Add(Global("double", OverloadIds.DoubleToDouble, [@double], @double, Conversions.DoubleIdentity, 1));
        table.Add(Global("double", OverloadIds.Int64ToDouble, [@int], @double, Conversions.Int64ToDouble, 1));
        table.Add(Global("double", OverloadIds.StringToDouble, [@string], @double, Conversions.StringToDouble, 1, Costs.Text));
        table.Add(Global("double", OverloadIds.UInt64ToDouble, [@uint], @double, Conversions.UInt64ToDouble, 1));
        TimeOverloads.AddDurationConversions(table, Source);
        table.Add(Global("dyn", OverloadIds.ToDyn, [A], CelType.Dyn, Conversions.ToDyn, 1));
        table.Add(Global("int", OverloadIds.Int64ToInt64, [@int], @int, Conversions.IntIdentity, 1));
        table.Add(Global("int", OverloadIds.DoubleToInt64, [@double], @int, Conversions.DoubleToInt64, 1));
        TimeOverloads.AddDurationToInt(table, Source);
        table.Add(Global("int", OverloadIds.StringToInt64, [@string], @int, Conversions.StringToInt64, 1, Costs.Text));
        TimeOverloads.AddTimestampToInt(table, Source);
        table.Add(Global("int", OverloadIds.UInt64ToInt64, [@uint], @int, Conversions.UInt64ToInt64, 1));
        table.Add(Global("string", OverloadIds.StringToString, [@string], @string, Conversions.StringIdentity, 1));
        table.Add(Global("string", OverloadIds.BoolToString, [@bool], @string, Conversions.BoolToString, 1));
        table.Add(Global("string", OverloadIds.BytesToString, [bytes], @string, Conversions.BytesToString, 1, Costs.Text));
        table.Add(Global("string", OverloadIds.DoubleToString, [@double], @string, Conversions.DoubleToString, 1));
        TimeOverloads.AddDurationToString(table, Source);
        table.Add(Global("string", OverloadIds.Int64ToString, [@int], @string, Conversions.Int64ToString, 1));
        TimeOverloads.AddTimestampToString(table, Source);
        table.Add(Global("string", OverloadIds.UInt64ToString, [@uint], @string, Conversions.UInt64ToString, 1));
        TimeOverloads.AddTimestampConversions(table, Source);
        table.Add(Global("uint", OverloadIds.UInt64ToUInt64, [@uint], @uint, Conversions.UIntIdentity, 1));
        table.Add(Global("uint", OverloadIds.DoubleToUInt64, [@double], @uint, Conversions.DoubleToUInt64, 1));
        table.Add(Global("uint", OverloadIds.Int64ToUInt64, [@int], @uint, Conversions.Int64ToUInt64, 1));
        table.Add(Global("uint", OverloadIds.StringToUInt64, [@string], @uint, Conversions.StringToUInt64, 1, Costs.Text));

        // Functions of strings, called on the string; they pass over both strings.
        table.Add(Member("contains", OverloadIds.ContainsString, [@string, @string], @bool, Strings.Contains, 1, Costs.Text));
        table.Add(Member("endsWith", OverloadIds.EndsWithString, [@string, @string], @bool, Strings.EndsWith, 1, Costs.Text));
        table.Add(Member("startsWith", OverloadIds.StartsWithString, [@string, @string], @bool, Strings.StartsWith, 1, Costs.Text));

        // The receivers of timestamp and duration.
        TimeOverloads.AddReceivers(table, Source);

        return table.DrainToImmutable();
    }

    // The overloads of one relation, then those of timestamp and duration, which come after bytes in cel-go; all of them run the
    // ordering of the library.
    private static void AddRelationWithTime(
        ImmutableArray<OverloadBinding>.Builder table,
        string function,
        string prefix,
        Relation relation,
        string timestampId,
        string durationId)
    {
        AddRelation(table, function, prefix, relation);
        TimeOverloads.AddRelations(
            table,
            Source,
            function,
            timestampId,
            durationId,
            Ordering(relation, CelValueKind.Timestamp, CelValueKind.Timestamp),
            Ordering(relation, CelValueKind.Duration, CelValueKind.Duration));
    }

    // The overloads of one relation: one for each ordered pair, those across numeric kinds candidates only
    // when an argument has the static type dyn. Strings and bytes cost 1 + ⌈Lₐ/10⌉, the others 1.
    private static void AddRelation(ImmutableArray<OverloadBinding>.Builder table, string function, string prefix, Relation relation)
    {
        foreach ((CelType left, CelType right) in OrderedPairs)
        {
            bool text = left.Kind is CelTypeKind.String or CelTypeKind.Bytes;
            bool across = !left.Equals(right);
            string id = prefix + "_" + Suffix(left) + (across ? "_" + Suffix(right) : string.Empty);
            table.Add(new OverloadBinding(
                function,
                new CelOverloadDecl(id, false, [left, right], CelType.Bool),
                Ordering(relation, Kind(left), Kind(right)),
                1,
                text ? Costs.Text : null,
                Source,
                dynOnly: across));
        }
    }

    // The part of an overload identifier that names a kind: int64, uint64 and the names of the other types.
    private static string Suffix(CelType type) => type.Kind switch
    {
        CelTypeKind.Int => "int64",
        CelTypeKind.UInt => "uint64",
        _ => type.ToString(),
    };

    private static CelValueKind Kind(CelType type) => type.Kind switch
    {
        CelTypeKind.Bool => CelValueKind.Bool,
        CelTypeKind.Int => CelValueKind.Int,
        CelTypeKind.UInt => CelValueKind.UInt,
        CelTypeKind.Double => CelValueKind.Double,
        CelTypeKind.String => CelValueKind.String,
        _ => CelValueKind.Bytes,
    };

    private static OverloadBinding Global(
        string function,
        string overloadId,
        ImmutableArray<CelType> parameterTypes,
        CelType resultType,
        OverloadImplementation? implementation,
        ulong fixedCost,
        CelCostFunction? argumentCost = null) =>
        new(function, new CelOverloadDecl(overloadId, false, parameterTypes, resultType), implementation, fixedCost, argumentCost, Source);

    // An overload called on its receiver, which is the first parameter.
    private static OverloadBinding Member(
        string function,
        string overloadId,
        ImmutableArray<CelType> parameterTypes,
        CelType resultType,
        OverloadImplementation implementation,
        ulong fixedCost,
        CelCostFunction? argumentCost = null) =>
        new(function, new CelOverloadDecl(overloadId, true, parameterTypes, resultType), implementation, fixedCost, argumentCost, Source);
}

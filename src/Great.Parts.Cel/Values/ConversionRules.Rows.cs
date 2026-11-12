using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Values;

/// <summary>
/// The rows of the result and expected-type tables as objects, one per .NET type: the expected CEL type of the .NET type,
/// whether it holds the values of a CEL type, whether a value that fits a CEL type can fail its conversion, and the
/// conversion itself.
/// </summary>
/// <remarks>
/// The rows are created once, for every .NET type of the tables: the primitive types, <c>CelValue</c>, <c>object</c>,
/// <c>CelList</c>, <c>CelMap</c>, <c>CelType</c>, <c>Nullable&lt;X&gt;</c> of the primitive value types, and
/// <c>IReadOnlyList&lt;X&gt;</c> and <c>IReadOnlyDictionary&lt;XK, XV&gt;</c> of these, with the keys of the CEL key
/// types. Native AOT creates no generic type at run time, so a collection type is a row only when this list names it.
/// </remarks>
internal static partial class ConversionRules
{
    // The row of T, found once per T; null outside the tables.
    private static class RowOf<T>
    {
        internal static readonly ClrRow<T>? Row = Rows.All.TryGetValue(typeof(T), out ClrRow? row) ? (ClrRow<T>)row : null;
    }

    private static class Rows
    {
        // The keys are .NET types of the library: trusted keys.
        internal static readonly FrozenDictionary<Type, ClrRow> All = Create();

        private static FrozenDictionary<Type, ClrRow> Create()
        {
            var boolRow = new PrimitiveRow<bool>(CelType.Bool, narrowing: false, takesNull: false);
            var longRow = new PrimitiveRow<long>(CelType.Int, narrowing: false, takesNull: false);
            var intRow = new PrimitiveRow<int>(CelType.Int, narrowing: true, takesNull: false);
            var ulongRow = new PrimitiveRow<ulong>(CelType.UInt, narrowing: false, takesNull: false);
            var uintRow = new PrimitiveRow<uint>(CelType.UInt, narrowing: true, takesNull: false);
            var doubleRow = new PrimitiveRow<double>(CelType.Double, narrowing: false, takesNull: false);
            var stringRow = new PrimitiveRow<string>(CelType.String, narrowing: false, takesNull: true);
            var memoryRow = new PrimitiveRow<ReadOnlyMemory<byte>>(CelType.Bytes, narrowing: false, takesNull: false);
            var arrayRow = new PrimitiveRow<byte[]>(CelType.Bytes, narrowing: false, takesNull: true);
            var keys = new KeyRows(longRow, intRow, ulongRow, uintRow, boolRow, stringRow);
#if NET11_0_OR_GREATER
            ClrRow[] decimalRows = DecimalRows.Create();
#else
            ClrRow[] decimalRows = [];
#endif
            ClrRow[] leaves =
            [
                boolRow, longRow, intRow, ulongRow, uintRow, doubleRow, stringRow, memoryRow, arrayRow,
                CelValueRow.Instance, ObjectRow.Instance,
                new ReferenceRow<CelList>(CelType.ListOf(CelType.Dyn), static (CelValue value, [NotNullWhen(true)] out CelList? list) => value.TryGetList(out list)),
                new ReferenceRow<CelMap>(CelType.MapOf(CelType.Dyn, CelType.Dyn), static (CelValue value, [NotNullWhen(true)] out CelMap? map) => value.TryGetMap(out map)),
                new ReferenceRow<CelType>(CelType.TypeOf(CelType.Dyn), static (CelValue value, [NotNullWhen(true)] out CelType? celType) => value.TryGetType(out celType)),
                new NullableRow<long>(longRow), new NullableRow<int>(intRow), new NullableRow<ulong>(ulongRow),
                new NullableRow<uint>(uintRow), new NullableRow<double>(doubleRow), new NullableRow<bool>(boolRow),
                new NullableRow<ReadOnlyMemory<byte>>(memoryRow),
                .. TimeRows.Create(),
                .. decimalRows,
            ];

            var rows = new Dictionary<Type, ClrRow>();
            foreach (ClrRow leaf in leaves)
            {
                rows.Add(leaf.ClrType, leaf);
                leaf.AddCollections(rows, keys);
            }

            return rows.ToFrozenDictionary();
        }
    }

    // The rows of the key types of maps: the CEL key types int, uint, bool and string, and dyn.
    private sealed class KeyRows(
        ClrRow<long> longRow,
        ClrRow<int> intRow,
        ClrRow<ulong> ulongRow,
        ClrRow<uint> uintRow,
        ClrRow<bool> boolRow,
        ClrRow<string> stringRow)
    {
        internal void AddMaps<TValue>(Dictionary<Type, ClrRow> rows, ClrRow<TValue> value)
        {
            Add(rows, longRow, value);
            Add(rows, intRow, value);
            Add(rows, ulongRow, value);
            Add(rows, uintRow, value);
            Add(rows, boolRow, value);
            Add(rows, stringRow, value);
            Add(rows, ObjectRow.Instance, value);
            Add(rows, CelValueRow.Instance, value);
        }

        private static void Add<TKey, TValue>(Dictionary<Type, ClrRow> rows, ClrRow<TKey> key, ClrRow<TValue> value)
            where TKey : notnull
        {
            var map = new MapRow<TKey, TValue>(key, value);
            rows.Add(typeof(IReadOnlyDictionary<TKey, TValue>), map);
            rows.Add(typeof(CelOptional<IReadOnlyDictionary<TKey, TValue>>), new OptionalRow<IReadOnlyDictionary<TKey, TValue>>(map));
        }
    }

    private abstract class ClrRow(CelType expectedType, bool takesNull)
    {
        // The expected CEL type of the .NET type.
        internal CelType ExpectedType { get; } = expectedType;

        internal abstract Type ClrType { get; }

        // Whether the .NET type takes null: a reference type or Nullable<X>.
        internal bool TakesNull { get; } = takesNull;

        // Whether the .NET type holds the values of a CEL type, for an explicit expected type: dyn and type parameters
        // always; null_type and wrapper(P) when the .NET type takes null and holds P; otherwise by the row.
        internal virtual bool Holds(CelType type) => type.Kind switch
        {
            CelTypeKind.Dyn or CelTypeKind.TypeParameter => true,
            CelTypeKind.Null => TakesNull,
            CelTypeKind.Wrapper => TakesNull && Holds(type.Parameters[0]),
            _ => HoldsCore(type),
        };

        // Whether every value that fits a CEL type converts to the .NET type: the static decision between a lazy view
        // and a copy. A value of type dyn can fail any row but those of CelValue and object.
        internal virtual bool CannotFail(CelType type) => type.Kind switch
        {
            CelTypeKind.Dyn or CelTypeKind.TypeParameter => false,
            CelTypeKind.Null => TakesNull,
            CelTypeKind.Wrapper => TakesNull && CannotFail(type.Parameters[0]),
            _ => CannotFailCore(type),
        };

        // Whether a value of the CEL type becomes a lazy view whose reads rely on that type, so that the facade checks
        // the value against it first.
        internal virtual bool IsCheckedView(CelType type) => false;

        // Whether a lazy view of this row reads a null element of a list or value of a map as null, so that the check
        // before it lets null through there: the element or value row takes null.
        internal virtual bool NullElements => false;

        // Adds the rows of IReadOnlyList<X> and IReadOnlyDictionary<XK, X> of a leaf row. A collection row adds none: the
        // tables nest one level, and a collection row of a collection row would make Native AOT compile such rows without end.
        internal virtual void AddCollections(Dictionary<Type, ClrRow> rows, KeyRows keys)
        {
        }

        private protected abstract bool HoldsCore(CelType type);

        private protected abstract bool CannotFailCore(CelType type);
    }

    private abstract class ClrRow<T>(CelType expectedType, bool takesNull) : ClrRow(expectedType, takesNull)
    {
        internal sealed override Type ClrType => typeof(T);

        // Converts a value that is not an error value and fits the static type. A collection that cannot fail is a lazy
        // view; another, and a byte array, is a copy, which the meter is charged first.
        internal abstract bool TryConvert(CelValue value, CelType type, IEvaluationMeter? meter, [MaybeNull] out T result);
    }

    // A row of a type that is not a collection of the tables: the element, key and value types of the collection rows.
    private abstract class LeafRow<T>(CelType expectedType, bool takesNull) : ClrRow<T>(expectedType, takesNull)
    {
        // The rows of the lists and maps of T, and the rows of CelOptional<…> of T, of its list, of its maps and of CelOptional<T> itself. The
        // optional of an optional of a collection is no row: the tables nest one level, and a row that names its own kind again
        // would make Native AOT compile such rows without end.
        internal sealed override void AddCollections(Dictionary<Type, ClrRow> rows, KeyRows keys)
        {
            var list = new ListRow<T>(this);
            var optional = new OptionalRow<T>(this);
            rows.Add(typeof(IReadOnlyList<T>), list);
            rows.Add(typeof(CelOptional<T>), optional);
            rows.Add(typeof(CelOptional<CelOptional<T>>), new OptionalRow<CelOptional<T>>(optional));
            rows.Add(typeof(CelOptional<IReadOnlyList<T>>), new OptionalRow<IReadOnlyList<T>>(list));
            keys.AddMaps(rows, this);
        }
    }

    // bool, the integers, double, string and bytes; int and uint narrow with a range check.
    private sealed class PrimitiveRow<T>(CelType expectedType, bool narrowing, bool takesNull) : LeafRow<T>(expectedType, takesNull)
    {
        internal override bool TryConvert(CelValue value, CelType type, IEvaluationMeter? meter, [MaybeNull] out T result) =>
            TryToPrimitive(value, meter, out result);

        private protected override bool HoldsCore(CelType type) => type.Kind == ExpectedType.Kind;

        private protected override bool CannotFailCore(CelType type) => !narrowing && type.Kind == ExpectedType.Kind;
    }

    // Nullable<X> of a primitive value type: null, or the value by the row of X.
    private sealed class NullableRow<T>(ClrRow<T> valueRow) : LeafRow<T?>(CelType.WrapperOf(valueRow.ExpectedType), takesNull: true)
        where T : struct
    {
        internal override bool TryConvert(CelValue value, CelType type, IEvaluationMeter? meter, [MaybeNull] out T? result)
        {
            if (value.Kind == CelValueKind.Null)
            {
                result = null;
                return true;
            }

            CelType valueType = type.Kind == CelTypeKind.Wrapper ? type.Parameters[0] : type;
            bool converted = valueRow.TryConvert(value, valueType, meter, out T inner);
            result = converted ? inner : null;
            return converted;
        }

        private protected override bool HoldsCore(CelType type) => valueRow.Holds(type);

        private protected override bool CannotFailCore(CelType type) => valueRow.CannotFail(type);
    }

    // CelValue: any value as it is.
    private sealed class CelValueRow() : LeafRow<CelValue>(CelType.Dyn, takesNull: true)
    {
        internal static readonly CelValueRow Instance = new();

        internal override bool Holds(CelType type) => true;

        internal override bool CannotFail(CelType type) => true;

        internal override bool TryConvert(CelValue value, CelType type, IEvaluationMeter? meter, out CelValue result)
        {
            result = value;
            return true;
        }

        private protected override bool HoldsCore(CelType type) => true;

        private protected override bool CannotFailCore(CelType type) => true;
    }

    // object: the natural mapping of any value; bytes as a copy, lists and maps as lazy views of objects.
    private sealed class ObjectRow() : LeafRow<object>(CelType.Dyn, takesNull: true)
    {
        internal static readonly ObjectRow Instance = new();

        internal override bool Holds(CelType type) => true;

        internal override bool CannotFail(CelType type) => true;

        internal override bool TryConvert(CelValue value, CelType type, IEvaluationMeter? meter, [MaybeNull] out object result)
        {
            result = null;
            switch (value.Kind)
            {
                case CelValueKind.Null:
                    return true;
                case CelValueKind.Bool when value.TryGetBoolean(out bool boolean):
                    result = boolean;
                    return true;
                case CelValueKind.Int when value.TryGetInt64(out long integer):
                    result = integer;
                    return true;
                case CelValueKind.UInt when value.TryGetUInt64(out ulong unsigned):
                    result = unsigned;
                    return true;
                case CelValueKind.Double when value.TryGetDouble(out double number):
                    result = number;
                    return true;
                case CelValueKind.String when value.TryGetString(out string? text):
                    result = text;
                    return true;
                case CelValueKind.Bytes when value.TryGetBytes(out ReadOnlyMemory<byte> bytes):
                    result = CopyBytes(bytes, meter);
                    return true;
                case CelValueKind.List when value.TryGetList(out CelList? list):
                    result = new ListView<object>(list, this, CelType.Dyn);
                    return true;
                case CelValueKind.Map when value.TryGetMap(out CelMap? map):
                    result = new MapView<object, object>(map, this, this, CelType.Dyn, CelType.Dyn);
                    return true;
                case CelValueKind.Type when value.TryGetType(out CelType? celType):
                    result = celType;
                    return true;
                default:
                    return TryConvertOther(value, meter, out result);
            }
        }

        // The kinds that are no scalar and no collection: time, an object, an abstract value and an optional.
        private bool TryConvertOther(CelValue value, IEvaluationMeter? meter, out object? result)
        {
            result = null;
            switch (value.Kind)
            {
                case CelValueKind.Timestamp or CelValueKind.Duration:
                    return TryBoxTime(value, out result);
#if NET11_0_OR_GREATER
                case CelValueKind.Decimal when value.TryGetDecimal(out System.Numerics.Decimal128 number):
                    result = number;
                    return true;
#endif
                case CelValueKind.Object when value.TryGetObject(out object? instance):
                    result = instance;
                    return true;
                case CelValueKind.Abstract:
                    // The natural mapping has no .NET type for an abstract value: it is the boxed CelValue.
                    result = value;
                    return true;
                case CelValueKind.Optional when value.TryGetOptional(out CelOptional<CelValue> optional):
                    return TryBoxOptional(optional, meter, out result);
                default:
                    return false;
            }
        }

        // An optional as the boxed CelOptional<object?>: the held value by the natural mapping of its kind.
        private bool TryBoxOptional(CelOptional<CelValue> optional, IEvaluationMeter? meter, out object? result)
        {
            if (!optional.HasValue)
            {
                result = default(CelOptional<object?>);
                return true;
            }

            if (!TryConvert(optional.Value, CelType.Dyn, meter, out object? held))
            {
                result = null;
                return false;
            }

            result = new CelOptional<object?>(held);
            return true;
        }

        // A timestamp or a duration as the boxed value of its own type.
        private static bool TryBoxTime(CelValue value, out object? result)
        {
            if (value.TryGetTimestamp(out CelTimestamp timestamp))
            {
                result = timestamp;
                return true;
            }

            if (value.TryGetDuration(out CelDuration duration))
            {
                result = duration;
                return true;
            }

            result = null;
            return false;
        }

        private protected override bool HoldsCore(CelType type) => true;

        private protected override bool CannotFailCore(CelType type) => true;
    }

    // Reads a value of the kind of a reference row.
    private delegate bool TryGetReference<T>(CelValue value, [NotNullWhen(true)] out T? result)
        where T : class;

    // CelList, CelMap and CelType: the value as it is, or null; a type value keeps the parameters that it holds.
    private sealed class ReferenceRow<T>(CelType expectedType, TryGetReference<T> tryGet) : LeafRow<T>(expectedType, takesNull: true)
        where T : class
    {
        internal override bool TryConvert(CelValue value, CelType type, IEvaluationMeter? meter, [MaybeNull] out T result)
        {
            result = null;
            return value.Kind == CelValueKind.Null || tryGet(value, out result);
        }

        private protected override bool HoldsCore(CelType type) => type.Kind == ExpectedType.Kind;

        private protected override bool CannotFailCore(CelType type) => type.Kind == ExpectedType.Kind;
    }

    // CelOptional<X>: the empty optional is default; an optional that holds a value holds the value converted by the row of X, which
    // reads it as an element of a copy is read: a position that takes null takes the null that the optional holds. An optional has no
    // view, so the held value is converted when the result is, and a refusal of the row of X is a refusal of the optional. A null value
    // is no optional. The held value of a collection row is a lazy view or a copy by the rule of that row.
    private sealed class OptionalRow<T>(ClrRow<T> held) : ClrRow<CelOptional<T>>(CelType.OptionalOf(held.ExpectedType), takesNull: false)
    {
        internal override bool IsCheckedView(CelType type) => type.Kind == CelTypeKind.Optional && held.IsCheckedView(type.Parameters[0]);

        internal override bool NullElements => held.TakesNull;

        internal override bool TryConvert(CelValue value, CelType type, IEvaluationMeter? meter, out CelOptional<T> result)
        {
            result = default;
            if (!value.TryGetOptional(out CelOptional<CelValue> optional))
            {
                return false;
            }

            if (!optional.HasValue)
            {
                return true;
            }

            CelType heldType = type.Kind == CelTypeKind.Optional ? type.Parameters[0] : CelType.Dyn;
            if (!held.TryConvert(optional.Value, heldType, meter, out T? converted))
            {
                return false;
            }

            result = new CelOptional<T>(converted!);
            return true;
        }

        private protected override bool HoldsCore(CelType type) => type.Kind == CelTypeKind.Optional && held.Holds(type.Parameters[0]);

        private protected override bool CannotFailCore(CelType type) => type.Kind == CelTypeKind.Optional && held.CannotFail(type.Parameters[0]);
    }

    // IReadOnlyList<X>: a lazy view when no element can fail, else an immutable copy.
    private sealed class ListRow<T>(ClrRow<T> element) : ClrRow<IReadOnlyList<T>>(CelType.ListOf(element.ExpectedType), takesNull: true)
    {
        internal override bool IsCheckedView(CelType type) => type.Kind == CelTypeKind.List && CannotFail(type);

        internal override bool NullElements => element.TakesNull;

        internal override bool TryConvert(CelValue value, CelType type, IEvaluationMeter? meter, [MaybeNull] out IReadOnlyList<T> result)
        {
            result = null;
            if (value.Kind == CelValueKind.Null)
            {
                return true;
            }

            if (!value.TryGetList(out CelList? list))
            {
                return false;
            }

            CelType elementType = type.Kind == CelTypeKind.List ? type.Parameters[0] : CelType.Dyn;
            if (type.Kind == CelTypeKind.List && element.CannotFail(elementType))
            {
                result = new ListView<T>(list, element, elementType);
                return true;
            }

            return TryCopy(list, element, elementType, meter, out result);
        }

        private protected override bool HoldsCore(CelType type) => type.Kind == CelTypeKind.List && element.Holds(type.Parameters[0]);

        private protected override bool CannotFailCore(CelType type) =>
            type.Kind == CelTypeKind.List && element.CannotFail(type.Parameters[0]);
    }

    // IReadOnlyDictionary<XK, XV>: a lazy view when no key and no value can fail, else an immutable copy.
    private sealed class MapRow<TKey, TValue>(ClrRow<TKey> keyRow, ClrRow<TValue> valueRow)
        : ClrRow<IReadOnlyDictionary<TKey, TValue>>(CelType.MapOf(keyRow.ExpectedType, valueRow.ExpectedType), takesNull: true)
        where TKey : notnull
    {
        internal override bool IsCheckedView(CelType type) => type.Kind == CelTypeKind.Map && CannotFail(type);

        // A key never takes null: null is no key of a map.
        internal override bool NullElements => valueRow.TakesNull;

        internal override bool TryConvert(CelValue value, CelType type, IEvaluationMeter? meter, [MaybeNull] out IReadOnlyDictionary<TKey, TValue> result)
        {
            result = null;
            if (value.Kind == CelValueKind.Null)
            {
                return true;
            }

            if (!value.TryGetMap(out CelMap? map))
            {
                return false;
            }

            bool isMap = type.Kind == CelTypeKind.Map;
            CelType keyType = isMap ? type.Parameters[0] : CelType.Dyn;
            CelType valueType = isMap ? type.Parameters[1] : CelType.Dyn;
            if (isMap && keyRow.CannotFail(keyType) && valueRow.CannotFail(valueType))
            {
                result = new MapView<TKey, TValue>(map, keyRow, valueRow, keyType, valueType);
                return true;
            }

            return TryCopy(map, keyRow, valueRow, keyType, valueType, meter, out result);
        }

        private protected override bool HoldsCore(CelType type) =>
            type.Kind == CelTypeKind.Map && keyRow.Holds(type.Parameters[0]) && valueRow.Holds(type.Parameters[1]);

        private protected override bool CannotFailCore(CelType type) =>
            type.Kind == CelTypeKind.Map && keyRow.CannotFail(type.Parameters[0]) && valueRow.CannotFail(type.Parameters[1]);
    }
}

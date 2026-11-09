using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Values;

/// <summary>
/// .NET values to CEL values, by the input rows of
/// the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see> for
/// values: integers, floating-point numbers, <c>bool</c>, <c>string</c>, <c>Guid</c>, enumerations, bytes,
/// <c>Nullable&lt;X&gt;</c> of these value types, <c>CelValue</c>, <c>CelList</c>, <c>CelMap</c> and <c>object</c>. The
/// conversion follows the static type <c>T</c> and does not box a value type, except a nullable enumeration.
/// </summary>
internal static partial class ConversionRules
{
    private static readonly CelType DynList = CelType.ListOf(CelType.Dyn);
    private static readonly CelType DynMap = CelType.MapOf(CelType.Dyn, CelType.Dyn);

    // The value of an enumeration on ulong above the range of int is the number.
    private static readonly Func<CelError, string> EnumOutOfRangeText =
        static error => ((ulong)error.Number).ToString(CultureInfo.InvariantCulture) + " is out of the range of int";

    /// <summary>
    /// The CEL type of a variable whose values have the .NET type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The .NET type.</typeparam>
    /// <returns>The CEL type; <see langword="null"/> when <typeparamref name="T"/> is not an input type yet.</returns>
    internal static CelType? InputType<T>() => InputType(typeof(T));

    /// <summary>
    /// The CEL type that the input rows give a result of .NET type <typeparamref name="T"/>: as <see cref="InputType{T}"/>, except
    /// that a list of a class of the application has no CEL type until Build, when the type index gives the object type.
    /// </summary>
    /// <typeparam name="T">The .NET type of the result.</typeparam>
    /// <returns>The CEL type; <see langword="null"/> when <typeparamref name="T"/> is not an input type yet or a list of a class of the application.</returns>
    internal static CelType? ResultInputType<T>() => ApplicationElementOf(typeof(T)) is null ? InputType<T>() : null;

    /// <summary>
    /// Converts a .NET value by its static type <typeparamref name="T"/>. A <see langword="null"/> reference or
    /// nullable value is the CEL <c>null</c>.
    /// </summary>
    /// <remarks>
    /// A value that the row refuses — an enumeration on <c>ulong</c> above the range of <c>int</c> — is an error value
    /// <see cref="CelErrorCode.ConversionRange"/> without a place: the reader of the value gives it the place of the read.
    /// </remarks>
    /// <typeparam name="T">The .NET type that selects the row.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="result">The CEL value when the method returns <see langword="true"/>; otherwise <c>null</c>.</param>
    /// <returns><see langword="true"/> when <typeparamref name="T"/> is an input type, the same as <see cref="InputType{T}"/>.</returns>
    /// <exception cref="NotImplementedException">
    /// <typeparamref name="T"/> is <see cref="object"/> and the value is of a .NET type outside the rows, which a later
    /// part of the library reads.
    /// </exception>
    internal static bool TryFromClr<T>(T value, out CelValue result)
    {
        if (IsSigned<T>())
        {
            result = CelValue.FromInt64(ToInt64(value));
            return true;
        }

        if (IsUnsigned<T>())
        {
            result = CelValue.FromUInt64(ToUInt64(value));
            return true;
        }

        if (typeof(T).IsEnum)
        {
            result = FromEnum(value);
            return true;
        }

        return TryFromOther(value, out result)
            || TryFromCelValueOrObject(value, out result)
            || TryFromNullable(value, out result)
            || TryFromJson(value, out result)
            || TryFromCollection(value, out result);
    }

    private static CelType? InputType(Type type) => LeafInputType(type) ?? CollectionInputType(type);

    // The CEL type of the rows that are not collections: the scalars, CelValue, object, JsonElement, CelList and CelMap,
    // and Nullable<X> of a scalar.
    private static CelType? LeafInputType(Type type)
    {
        if (ScalarInputType(type) is CelType scalar)
        {
            return scalar;
        }

        if (type == typeof(CelValue) || type == typeof(object) || type == typeof(JsonElement))
        {
            return CelType.Dyn;
        }

        if (type == typeof(CelList))
        {
            return DynList;
        }

        if (type == typeof(CelMap))
        {
            return DynMap;
        }

        // Nullable<X> of a value type with a wrapper: the wrapper of the CEL type of X.
        return Nullable.GetUnderlyingType(type) is Type underlying && ScalarInputType(underlying) is CelType primitive
            ? CelType.WrapperOf(primitive)
            : null;
    }

    // The rows of numbers, enumerations, bool, string, Guid and bytes.
    private static CelType? ScalarInputType(Type type)
    {
        if (IsSigned(type) || type.IsEnum)
        {
            return CelType.Int;
        }

        if (IsUnsigned(type))
        {
            return CelType.UInt;
        }

        if (type == typeof(double) || type == typeof(float) || type == typeof(Half))
        {
            return CelType.Double;
        }

        if (type == typeof(bool))
        {
            return CelType.Bool;
        }

        if (type == typeof(string) || type == typeof(Guid))
        {
            return CelType.String;
        }

        return type == typeof(byte[]) || type == typeof(ReadOnlyMemory<byte>) ? CelType.Bytes : null;
    }

    private static bool IsSigned(Type type) =>
        type == typeof(long) || type == typeof(int) || type == typeof(short) || type == typeof(sbyte) || type == typeof(nint);

    private static bool IsUnsigned(Type type) =>
        type == typeof(ulong) || type == typeof(uint) || type == typeof(ushort) || type == typeof(byte) || type == typeof(nuint);

    // The same tests on the static type, which the JIT and the AOT compiler fold to constants.
    private static bool IsSigned<T>() =>
        typeof(T) == typeof(long) || typeof(T) == typeof(int) || typeof(T) == typeof(short) || typeof(T) == typeof(sbyte) || typeof(T) == typeof(nint);

    private static bool IsUnsigned<T>() =>
        typeof(T) == typeof(ulong) || typeof(T) == typeof(uint) || typeof(T) == typeof(ushort) || typeof(T) == typeof(byte) || typeof(T) == typeof(nuint);

    // Each branch reinterprets the value as the type the test proved, so the conversion of a value type does not box it.
    private static long ToInt64<T>(T value)
    {
        if (typeof(T) == typeof(int))
        {
            return Unsafe.As<T, int>(ref value);
        }

        if (typeof(T) == typeof(short))
        {
            return Unsafe.As<T, short>(ref value);
        }

        if (typeof(T) == typeof(sbyte))
        {
            return Unsafe.As<T, sbyte>(ref value);
        }

        return typeof(T) == typeof(nint) ? Unsafe.As<T, nint>(ref value) : Unsafe.As<T, long>(ref value);
    }

    private static ulong ToUInt64<T>(T value)
    {
        if (typeof(T) == typeof(uint))
        {
            return Unsafe.As<T, uint>(ref value);
        }

        if (typeof(T) == typeof(ushort))
        {
            return Unsafe.As<T, ushort>(ref value);
        }

        if (typeof(T) == typeof(byte))
        {
            return Unsafe.As<T, byte>(ref value);
        }

        return typeof(T) == typeof(nuint) ? Unsafe.As<T, nuint>(ref value) : Unsafe.As<T, ulong>(ref value);
    }

    // An enumeration by its underlying value; an enumeration on ulong above the range of int is out of range.
    private static CelValue FromEnum<T>(T value)
    {
        switch (Type.GetTypeCode(typeof(T)))
        {
            case TypeCode.SByte:
                return CelValue.FromInt64(Unsafe.As<T, sbyte>(ref value));
            case TypeCode.Byte:
                return CelValue.FromInt64(Unsafe.As<T, byte>(ref value));
            case TypeCode.Int16:
                return CelValue.FromInt64(Unsafe.As<T, short>(ref value));
            case TypeCode.UInt16:
                return CelValue.FromInt64(Unsafe.As<T, ushort>(ref value));
            case TypeCode.Int32:
                return CelValue.FromInt64(Unsafe.As<T, int>(ref value));
            case TypeCode.UInt32:
                return CelValue.FromInt64(Unsafe.As<T, uint>(ref value));
            case TypeCode.Int64:
                return CelValue.FromInt64(Unsafe.As<T, long>(ref value));
            case TypeCode.UInt64:
                return FromUnsignedEnum(Unsafe.As<T, ulong>(ref value));
            default:
                // C# declares enumerations only on the integral types above.
                throw new NotImplementedException();
        }
    }

    // A boxed enumeration, as object or as a nullable enumeration.
    private static CelValue FromBoxedEnum(Enum value)
    {
        IConvertible number = value;
        return Type.GetTypeCode(value.GetType()) == TypeCode.UInt64
            ? FromUnsignedEnum(number.ToUInt64(CultureInfo.InvariantCulture))
            : CelValue.FromInt64(number.ToInt64(CultureInfo.InvariantCulture));
    }

    private static CelValue FromUnsignedEnum(ulong value) => value <= long.MaxValue
        ? CelValue.FromInt64((long)value)
        : CelValue.FromError(new CelError(CelErrorCode.ConversionRange, EnumOutOfRangeText, null, 0, number: (long)value));

    // double, float, Half, bool, string, Guid and bytes.
    private static bool TryFromOther<T>(T value, out CelValue result)
    {
        result = default;
        if (typeof(T) == typeof(double))
        {
            result = CelValue.FromDouble(Unsafe.As<T, double>(ref value));
        }
        else if (typeof(T) == typeof(float))
        {
            result = CelValue.FromDouble(Unsafe.As<T, float>(ref value));
        }
        else if (typeof(T) == typeof(Half))
        {
            result = CelValue.FromDouble((double)Unsafe.As<T, Half>(ref value));
        }
        else if (typeof(T) == typeof(bool))
        {
            result = CelValue.FromBoolean(Unsafe.As<T, bool>(ref value));
        }
        else if (typeof(T) == typeof(Guid))
        {
            result = FromGuid(Unsafe.As<T, Guid>(ref value));
        }
        else if (typeof(T) == typeof(ReadOnlyMemory<byte>))
        {
            result = CelValue.FromBytes(Unsafe.As<T, ReadOnlyMemory<byte>>(ref value));
        }
        else if (typeof(T) == typeof(string))
        {
            result = value is string text ? CelValue.FromString(text) : CelValue.Null;
        }
        else if (typeof(T) == typeof(byte[]))
        {
            result = value is byte[] bytes ? CelValue.FromBytes(bytes) : CelValue.Null;
        }
        else
        {
            return false;
        }

        return true;
    }

    // CelValue as it is; CelList and CelMap as a list and a map; object by the row of its runtime type.
    private static bool TryFromCelValueOrObject<T>(T value, out CelValue result)
    {
        if (typeof(T) == typeof(CelValue))
        {
            result = Unsafe.As<T, CelValue>(ref value);
        }
        else if (typeof(T) == typeof(CelList))
        {
            result = value is CelList list ? CelValue.FromList(list) : CelValue.Null;
        }
        else if (typeof(T) == typeof(CelMap))
        {
            result = value is CelMap map ? CelValue.FromMap(map) : CelValue.Null;
        }
        else if (typeof(T) == typeof(object))
        {
            result = FromObject(value);
        }
        else
        {
            result = default;
            return false;
        }

        return true;
    }

    // Nullable<X> of a value type of the rows: null, or the value of X.
    private static bool TryFromNullable<T>(T value, out CelValue result)
    {
        if (TryFromNullableInteger(value, out result))
        {
            return true;
        }

        if (typeof(T) == typeof(double?))
        {
            return FromNullable(Unsafe.As<T, double?>(ref value), out result);
        }

        if (typeof(T) == typeof(float?))
        {
            return FromNullable(Unsafe.As<T, float?>(ref value), out result);
        }

        if (typeof(T) == typeof(Half?))
        {
            return FromNullable(Unsafe.As<T, Half?>(ref value), out result);
        }

        if (typeof(T) == typeof(bool?))
        {
            return FromNullable(Unsafe.As<T, bool?>(ref value), out result);
        }

        if (typeof(T) == typeof(Guid?))
        {
            return FromNullable(Unsafe.As<T, Guid?>(ref value), out result);
        }

        if (typeof(T) == typeof(ReadOnlyMemory<byte>?))
        {
            return FromNullable(Unsafe.As<T, ReadOnlyMemory<byte>?>(ref value), out result);
        }

        // A nullable enumeration boxes to its enumeration or to null.
        if (Nullable.GetUnderlyingType(typeof(T)) is { IsEnum: true })
        {
            result = value is Enum number ? FromBoxedEnum(number) : CelValue.Null;
            return true;
        }

        return false;
    }

    // Nullable<X> of an integer type.
    private static bool TryFromNullableInteger<T>(T value, out CelValue result)
    {
        result = default;
        if (typeof(T) == typeof(long?))
        {
            return FromNullable(Unsafe.As<T, long?>(ref value), out result);
        }

        if (typeof(T) == typeof(int?))
        {
            return FromNullable(Unsafe.As<T, int?>(ref value), out result);
        }

        if (typeof(T) == typeof(short?))
        {
            return FromNullable(Unsafe.As<T, short?>(ref value), out result);
        }

        if (typeof(T) == typeof(sbyte?))
        {
            return FromNullable(Unsafe.As<T, sbyte?>(ref value), out result);
        }

        if (typeof(T) == typeof(nint?))
        {
            return FromNullable(Unsafe.As<T, nint?>(ref value), out result);
        }

        if (typeof(T) == typeof(ulong?))
        {
            return FromNullable(Unsafe.As<T, ulong?>(ref value), out result);
        }

        if (typeof(T) == typeof(uint?))
        {
            return FromNullable(Unsafe.As<T, uint?>(ref value), out result);
        }

        if (typeof(T) == typeof(ushort?))
        {
            return FromNullable(Unsafe.As<T, ushort?>(ref value), out result);
        }

        if (typeof(T) == typeof(byte?))
        {
            return FromNullable(Unsafe.As<T, byte?>(ref value), out result);
        }

        return typeof(T) == typeof(nuint?) && FromNullable(Unsafe.As<T, nuint?>(ref value), out result);
    }

    private static bool FromNullable<TValue>(TValue? value, out CelValue result)
        where TValue : struct
    {
        if (!value.HasValue)
        {
            result = CelValue.Null;
            return true;
        }

        return TryFromClr(value.GetValueOrDefault(), out result);
    }

    private static CelValue FromGuid(Guid value) => CelValue.FromString(value.ToString("D", CultureInfo.InvariantCulture));

    /// <summary>
    /// Tells whether a value is a .NET collection that the <c>object</c> row does not read: an enumerable other than a
    /// string, bytes, <see cref="CelList"/> and <see cref="CelMap"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> for a .NET collection.</returns>
    internal static bool IsClrCollection(object? value) =>
        value is System.Collections.IEnumerable and not (string or byte[] or CelList or CelMap);

    // A value in the place of dyn, by the row of its runtime type: a .NET collection is TypeNotAccessible without a
    // place; any other instance outside the rows is an object value without a description.
    private static CelValue FromObject(object? value)
    {
        if (TryFromObject(value, out CelValue result))
        {
            return result;
        }

        // Time and decimals come with their parts. Any other instance, a .NET collection included, is a bare object value
        // that the node which reads it decides with the type index.
        return IsOfLaterPart(value!) ? throw new NotImplementedException() : CelValue.FromBareObject(value!);
    }

    // The instances of the input rows that later parts read: JSON, time and decimals.
    private static bool IsOfLaterPart(object value) =>
        value is CelTimestamp or DateTimeOffset or DateTime or CelDuration or TimeSpan or decimal
#if NET11_0_OR_GREATER
            or System.Numerics.Decimal128
#endif
        ;

    // The same, false for a .NET type outside the rows.
    private static bool TryFromObject(object? value, out CelValue result)
    {
        CelValue? converted = value switch
        {
            null => CelValue.Null,
            CelValue celValue => celValue,
            CelList list => CelValue.FromList(list),
            CelMap map => CelValue.FromMap(map),
            string text => CelValue.FromString(text),
            byte[] bytes => CelValue.FromBytes(bytes),
            ReadOnlyMemory<byte> memory => CelValue.FromBytes(memory),
            bool boolean => CelValue.FromBoolean(boolean),
            long number => CelValue.FromInt64(number),
            int number => CelValue.FromInt64(number),
            short number => CelValue.FromInt64(number),
            sbyte number => CelValue.FromInt64(number),
            nint number => CelValue.FromInt64(number),
            ulong number => CelValue.FromUInt64(number),
            uint number => CelValue.FromUInt64(number),
            ushort number => CelValue.FromUInt64(number),
            byte number => CelValue.FromUInt64(number),
            nuint number => CelValue.FromUInt64(number),
            double number => CelValue.FromDouble(number),
            float number => CelValue.FromDouble(number),
            Half number => CelValue.FromDouble((double)number),
            Guid guid => FromGuid(guid),
            JsonElement element => JsonRules.FromElement(element),
            Enum number => FromBoxedEnum(number),
            _ => null,
        };

        result = converted.GetValueOrDefault();
        return converted.HasValue;
    }
}

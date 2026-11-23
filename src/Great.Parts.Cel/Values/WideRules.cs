using System;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Values;

// char, Int128, UInt128 and BigInteger as input: char is a string of one UTF-16 unit, the integers wider than 64 bits are int and uint
// when the value fits and the error ConversionRange when it does not. The rows of these types as results are in ConversionRules.
internal static class WideRules
{
    // A number that CEL cannot hold. The first part is the number itself, boxed on the path of the error only; the second is the
    // name of the field that the description reads, or null.
    private static readonly ErrorTemplate RangeText = new(
        static error => NumberText(error) + " is out of the range of " + TargetName(error),
        subject: static error => error.Second as string,
        expectedType: static error => TargetType(error),
        actualType: static error => TargetType(error));

    // The same number as the result of an application function: the second part names the function and its overload.
    private static readonly ErrorTemplate ResultText = new(
        static error => "the result of overload '" + ((ResultNames)error.Second!).OverloadId + "' of function '" + ((ResultNames)error.Second!).Function
            + "' cannot be read: " + NumberText(error) + " is out of the range of " + TargetName(error),
        subject: static error => ((ResultNames)error.Second!).Function,
        expectedType: static error => TargetType(error),
        actualType: static error => TargetType(error));

    private static string NumberText(CelError error) => ((IFormattable)error.First!).ToString(null, CultureInfo.InvariantCulture);

    private static string TargetName(CelError error) => error.First is UInt128 ? "uint" : "int";

    private static CelType TargetType(CelError error) => error.First is UInt128 ? CelType.UInt : CelType.Int;

    /// <summary>
    /// The CEL type of char and of the wide integers of the input rows, or <see langword="null"/> for another .NET type.
    /// </summary>
    /// <param name="type">The .NET type.</param>
    /// <returns>The CEL type, or <see langword="null"/>.</returns>
    internal static CelType? InputType(Type type)
    {
        if (type == typeof(char))
        {
            return CelType.String;
        }

        if (type == typeof(Int128) || type == typeof(BigInteger))
        {
            return CelType.Int;
        }

        return type == typeof(UInt128) ? CelType.UInt : null;
    }

    /// <summary>
    /// The string of a <see cref="char"/>: one UTF-16 unit; a lone surrogate, which a CEL string cannot hold, is an error value
    /// <c>InvalidArgument</c> that has no place.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="name">What the message names, or <see langword="null"/> when the reader of the value names it.</param>
    /// <returns>A string or the error value.</returns>
    internal static CelValue FromChar(char value, string? name = null) => char.IsSurrogate(value)
        ? CelValue.FromError(new CelError(
            CelErrorCode.InvalidArgument,
            CelError.FirstPart,
            null,
            0,
            ConversionRules.Refusal(name, "a char that is a lone surrogate (U+" + ((int)value).ToString("X4", CultureInfo.InvariantCulture) + "), which a CEL string cannot hold"),
            name is null ? ConversionRules.UnnamedRefusal : null))
        : CelValue.FromString(new string(value, 1));

    internal static CelValue FromInt128(Int128 value, string? name = null) =>
        TryFromInt128(value, out CelValue result) ? result : OutOfRange(value, name);

    internal static CelValue FromUInt128(UInt128 value, string? name = null) =>
        TryFromUInt128(value, out CelValue result) ? result : OutOfRange(value, name);

    internal static CelValue FromBigInteger(BigInteger value, string? name = null) =>
        TryFromBigInteger(value, out CelValue result) ? result : OutOfRange(value, name);

    // The conversions that fit, which allocate nothing. A BigInteger is tested by its number of bits, never by a copy.
    internal static bool TryFromInt128(Int128 value, out CelValue result)
    {
        bool fits = value >= long.MinValue && value <= long.MaxValue;
        result = fits ? CelValue.FromInt64((long)value) : default;
        return fits;
    }

    internal static bool TryFromUInt128(UInt128 value, out CelValue result)
    {
        bool fits = value <= ulong.MaxValue;
        result = fits ? CelValue.FromUInt64((ulong)value) : default;
        return fits;
    }

    internal static bool TryFromBigInteger(BigInteger value, out CelValue result)
    {
        bool fits = value.GetBitLength() <= 63;
        result = fits ? CelValue.FromInt64((long)value) : default;
        return fits;
    }

    /// <summary>
    /// The error value of a number wider than 64 bits that CEL cannot hold, without a place.
    /// </summary>
    /// <param name="value">The <see cref="Int128"/>, <see cref="UInt128"/> or <see cref="BigInteger"/>.</param>
    /// <param name="name">The name of the field the error is about, or <see langword="null"/>.</param>
    /// <returns>The error value.</returns>
    internal static CelValue OutOfRange(object value, string? name) =>
        CelValue.FromError(new CelError(CelErrorCode.ConversionRange, RangeText, null, 0, value, name));

    /// <summary>
    /// Names the function and the overload in the error of a number wider than 64 bits that an application function returned. Any other
    /// value is returned as it is.
    /// </summary>
    /// <param name="value">The value the result converted to.</param>
    /// <param name="function">The name of the function.</param>
    /// <param name="overloadId">The identifier of the overload.</param>
    /// <returns>The value, or the error value with the names.</returns>
    internal static CelValue NameResult(CelValue value, string function, string overloadId) =>
        IsOutOfRange(value) && value.TryGetError(out CelError? error)
            ? CelValue.FromError(new CelError(CelErrorCode.ConversionRange, ResultText, null, 0, error.First, new ResultNames(function, overloadId)))
            : value;

    /// <summary>
    /// Tells whether a value is the error of a number wider than 64 bits outside the range of <c>int</c> or <c>uint</c>.
    /// </summary>
    /// <param name="value">The result of a conversion.</param>
    /// <returns><see langword="true"/> for that error value.</returns>
    internal static bool IsOutOfRange(CelValue value) =>
        value.Kind == CelValueKind.Error
        && value.TryGetError(out CelError? error)
        && error.Code == CelErrorCode.ConversionRange
        && error.First is Int128 or UInt128 or BigInteger;

    /// <summary>
    /// A boxed char or wide integer in a place of the type <c>dyn</c>; <see langword="null"/> for a value of another type.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The CEL value, possibly the error value, or <see langword="null"/>.</returns>
    internal static CelValue? FromBoxed(object? value) => value switch
    {
        char character => FromChar(character),
        Int128 number => FromInt128(number),
        UInt128 number => FromUInt128(number),
        BigInteger number => FromBigInteger(number),
        _ => null,
    };

    private sealed record ResultNames(string Function, string OverloadId);

    /// <summary>
    /// char, Int128, UInt128 and BigInteger and the Nullable of each, by their static type.
    /// </summary>
    /// <typeparam name="T">The static type of the value.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="result">The CEL value when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when <typeparamref name="T"/> is one of these types.</returns>
    internal static bool TryFrom<T>(T value, out CelValue result)
    {
        if (typeof(T) == typeof(char))
        {
            result = FromChar(Unsafe.As<T, char>(ref value));
        }
        else if (typeof(T) == typeof(Int128))
        {
            result = FromInt128(Unsafe.As<T, Int128>(ref value));
        }
        else if (typeof(T) == typeof(UInt128))
        {
            result = FromUInt128(Unsafe.As<T, UInt128>(ref value));
        }
        else if (typeof(T) == typeof(BigInteger))
        {
            result = FromBigInteger(Unsafe.As<T, BigInteger>(ref value));
        }
        else
        {
            return TryFromNullable(value, out result);
        }

        return true;
    }

    // Nullable<X> of those: null, or the value of X.
    private static bool TryFromNullable<T>(T value, out CelValue result)
    {
        if (typeof(T) == typeof(char?))
        {
            char? held = Unsafe.As<T, char?>(ref value);
            result = held.HasValue ? FromChar(held.GetValueOrDefault()) : CelValue.Null;
        }
        else if (typeof(T) == typeof(Int128?))
        {
            Int128? held = Unsafe.As<T, Int128?>(ref value);
            result = held.HasValue ? FromInt128(held.GetValueOrDefault()) : CelValue.Null;
        }
        else if (typeof(T) == typeof(UInt128?))
        {
            UInt128? held = Unsafe.As<T, UInt128?>(ref value);
            result = held.HasValue ? FromUInt128(held.GetValueOrDefault()) : CelValue.Null;
        }
        else if (typeof(T) == typeof(BigInteger?))
        {
            BigInteger? held = Unsafe.As<T, BigInteger?>(ref value);
            result = held.HasValue ? FromBigInteger(held.GetValueOrDefault()) : CelValue.Null;
        }
        else
        {
            result = default;
            return false;
        }

        return true;
    }
}

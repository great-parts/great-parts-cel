using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel.Values;

/// <summary>
/// CEL values to the requested .NET type, by the result rows of the type mapping table for the simple
/// types so far. There is no conversion between numeric types: a <c>uint</c> is not a <c>long</c>.
/// </summary>
internal static partial class ConversionRules
{
    /// <summary>
    /// Tells whether <typeparamref name="T"/> is a result type so far: <c>bool</c>, <c>long</c>,
    /// <c>int</c>, <c>ulong</c>, <c>uint</c>, <c>double</c>, <c>string</c>, <c>ReadOnlyMemory&lt;byte&gt;</c>,
    /// <c>byte[]</c> or <see cref="CelValue"/>.
    /// </summary>
    /// <typeparam name="T">The requested .NET type.</typeparam>
    /// <returns><see langword="true"/> for the types of the rows.</returns>
    internal static bool IsResultType<T>() =>
        typeof(T) == typeof(bool) || typeof(T) == typeof(long) || typeof(T) == typeof(int) || typeof(T) == typeof(ulong)
        || typeof(T) == typeof(uint) || typeof(T) == typeof(double) || typeof(T) == typeof(string)
        || typeof(T) == typeof(ReadOnlyMemory<byte>) || typeof(T) == typeof(byte[]) || typeof(T) == typeof(CelValue);

    /// <summary>
    /// Converts a value that is not an error value to <typeparamref name="T"/> by the row of its CEL type.
    /// </summary>
    /// <typeparam name="T">The requested .NET type.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="result">
    /// The converted value when the method returns <see langword="true"/>, <see langword="null"/> for a CEL
    /// <c>null</c> as a string or a byte array; otherwise the default value.
    /// </param>
    /// <returns><see langword="false"/> when no row converts the value to <typeparamref name="T"/>.</returns>
    internal static bool TryToClr<T>(CelValue value, [MaybeNull] out T result)
    {
        if (typeof(T) == typeof(CelValue))
        {
            result = Unsafe.As<CelValue, T>(ref value);
            return true;
        }

        if (value.Kind == CelValueKind.Null && (typeof(T) == typeof(string) || typeof(T) == typeof(byte[])))
        {
            result = default;
            return true;
        }

        return TryToInteger(value, out result) || TryToOther(value, out result);
    }

    /// <summary>
    /// Creates the error <see cref="CelErrorCode.ResultConversion"/> for a value that
    /// <see cref="TryToClr{T}"/> does not convert.
    /// </summary>
    /// <typeparam name="T">The requested .NET type.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="location">The position of the root of the expression.</param>
    /// <returns>The error of the stage <see cref="CelErrorStage.Evaluate"/>.</returns>
    internal static CelError ResultConversionError<T>(CelValue value, CelSourceLocation location)
    {
        string clrType = typeof(T) == typeof(ReadOnlyMemory<byte>) ? "System.ReadOnlyMemory<System.Byte>" : typeof(T).ToString();
        string message = string.Create(
            CultureInfo.InvariantCulture,
            $"result of type '{ValueTypeNames.Of(value.Kind)}' cannot be converted to .NET type '{clrType}'");
        return new CelError(CelErrorCode.ResultConversion, CelErrorStage.Evaluate, message, location, null, null);
    }

    // long and int from an int, ulong and uint from a uint, each with the range check of the narrower type.
    private static bool TryToInteger<T>(CelValue value, [MaybeNull] out T result)
    {
        result = default;
        if (value.TryGetInt64(out long intValue))
        {
            if (typeof(T) == typeof(long))
            {
                result = Unsafe.As<long, T>(ref intValue);
                return true;
            }

            if (typeof(T) == typeof(int) && intValue is >= int.MinValue and <= int.MaxValue)
            {
                int narrow = (int)intValue;
                result = Unsafe.As<int, T>(ref narrow);
                return true;
            }
        }
        else if (value.TryGetUInt64(out ulong uintValue))
        {
            if (typeof(T) == typeof(ulong))
            {
                result = Unsafe.As<ulong, T>(ref uintValue);
                return true;
            }

            if (typeof(T) == typeof(uint) && uintValue <= uint.MaxValue)
            {
                uint narrow = (uint)uintValue;
                result = Unsafe.As<uint, T>(ref narrow);
                return true;
            }
        }

        return false;
    }

    // bool, double, string and bytes, each only from its own CEL type.
    private static bool TryToOther<T>(CelValue value, [MaybeNull] out T result)
    {
        result = default;
        if (typeof(T) == typeof(bool) && value.TryGetBoolean(out bool boolValue))
        {
            result = Unsafe.As<bool, T>(ref boolValue);
        }
        else if (typeof(T) == typeof(double) && value.TryGetDouble(out double doubleValue))
        {
            result = Unsafe.As<double, T>(ref doubleValue);
        }
        else if (typeof(T) == typeof(string) && value.TryGetString(out string? text))
        {
            result = (T)(object)text;
        }
        else if (typeof(T) == typeof(ReadOnlyMemory<byte>) && value.TryGetBytes(out ReadOnlyMemory<byte> memory))
        {
            result = Unsafe.As<ReadOnlyMemory<byte>, T>(ref memory);
        }
        else if (typeof(T) == typeof(byte[]) && value.TryGetBytes(out ReadOnlyMemory<byte> copied))
        {
            result = (T)(object)copied.ToArray();
        }
        else
        {
            return false;
        }

        return true;
    }
}

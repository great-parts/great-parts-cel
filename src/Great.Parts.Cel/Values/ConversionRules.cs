using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Values;

/// <summary>
/// .NET values of simple types to CEL values, by the input rows of the type mapping table: the types that
/// variables and bindings accept so far. The conversion follows the static type <c>T</c> and does not box
/// a value type.
/// </summary>
internal static partial class ConversionRules
{
    /// <summary>
    /// The CEL type of a variable whose values have the .NET type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The .NET type.</typeparam>
    /// <returns>The CEL type; <see langword="null"/> when <typeparamref name="T"/> is not an input type yet.</returns>
    internal static CelType? InputType<T>()
    {
        if (IsSigned<T>())
        {
            return CelType.Int;
        }

        if (IsUnsigned<T>())
        {
            return CelType.UInt;
        }

        if (typeof(T) == typeof(double) || typeof(T) == typeof(float) || typeof(T) == typeof(Half))
        {
            return CelType.Double;
        }

        if (typeof(T) == typeof(bool))
        {
            return CelType.Bool;
        }

        if (typeof(T) == typeof(string) || typeof(T) == typeof(Guid))
        {
            return CelType.String;
        }

        return typeof(T) == typeof(byte[]) || typeof(T) == typeof(ReadOnlyMemory<byte>) ? CelType.Bytes : null;
    }

    /// <summary>
    /// Converts a .NET value by its static type <typeparamref name="T"/>. A <see langword="null"/> string or
    /// byte array is the CEL <c>null</c>.
    /// </summary>
    /// <typeparam name="T">The .NET type that selects the row.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="result">The CEL value when the method returns <see langword="true"/>; otherwise <c>null</c>.</param>
    /// <returns><see langword="true"/> when <typeparamref name="T"/> is an input type, the same as <see cref="InputType{T}"/>.</returns>
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

        return TryFromOther(value, out result);
    }

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
            result = CelValue.FromString(Unsafe.As<T, Guid>(ref value).ToString("D", CultureInfo.InvariantCulture));
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
}

using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
#if NET11_0_OR_GREATER
using System.Numerics;
using Great.Parts.Cel.Diagnostics;
#endif
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Values;

// The rows of decimal: System.Decimal and its Nullable<>, and on net11.0 Decimal128 and its Nullable<>, as input and as result. The CEL
// type is decimal for all of them. A typed collection of decimals is not a row (the closed list of elements has no decimal), as it is
// not for time.
internal static partial class ConversionRules
{
#if NET11_0_OR_GREATER
    // The texts of the error of a decimal that is a NaN or an infinity, by the first part of the error.
    private static readonly ErrorTemplate NotFiniteText = new(
        static error => (string)error.First! + " is out of the range of decimal",
        expectedType: static _ => CelType.Decimal,
        actualType: static _ => CelType.Decimal);

    /// <summary>
    /// The CEL value of a <see cref="Decimal128"/>: a decimal, or for a NaN and an infinity, which CEL has no value for, the error value
    /// <c>ConversionRange</c> <c>NaN is out of the range of decimal</c> that has no place.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A decimal or the error value.</returns>
    internal static CelValue FromDecimal128(Decimal128 value) =>
        Decimal128.IsFinite(value) ? CelValue.FromDecimal128(value) : CelValue.FromError(new CelError(CelErrorCode.ConversionRange, NotFiniteText, null, 0, NotFiniteName(value)));
#endif

    /// <summary>
    /// The decimal of a <see cref="decimal"/>, exactly, with its scale as the exponent and the sign of its zero.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The decimal.</returns>
    internal static CelValue FromSystemDecimal(decimal value) => CelValue.FromDecimal(value);

    // A boxed decimal, a System.Decimal or on net11.0 a Decimal128, in a place of the type dyn; null for a value of another type.
    private static CelValue? FromBoxedDecimal(object? value) => value switch
    {
#if NET11_0_OR_GREATER
        Decimal128 number => FromDecimal128(number),
        decimal number => CelValue.FromSystemDecimalOrigin(DecimalNumberRules.FromSystemDecimal(number).ToDecimal128()),
#else
        decimal number => CelValue.FromDecimal(number),
#endif
        _ => null,
    };

    /// <summary>
    /// Tells whether the conversion of a binding of the static type <typeparamref name="T"/> met a NaN or an infinity of a
    /// <c>Decimal128</c>, which <c>CelBindings.Add</c> refuses; always <see langword="false"/> on net10.0, which has no
    /// <c>Decimal128</c>.
    /// </summary>
    /// <typeparam name="T">The static type of the binding.</typeparam>
    /// <param name="converted">The result of the conversion.</param>
    /// <returns><see langword="true"/> for an error value of <c>Decimal128</c> or <c>Decimal128?</c>.</returns>
    internal static bool IsNotFiniteDecimal<T>(CelValue converted) =>
#if NET11_0_OR_GREATER
        converted.Kind == CelValueKind.Error && (typeof(T) == typeof(Decimal128) || typeof(T) == typeof(Decimal128?));
#else
        false;
#endif

    // System.Decimal, on net11.0 Decimal128, and their Nullable<> by their static type.
    private static bool TryFromDecimal<T>(T value, out CelValue result)
    {
#if NET11_0_OR_GREATER
        if (typeof(T) == typeof(Decimal128))
        {
            result = FromDecimal128(Unsafe.As<T, Decimal128>(ref value));
        }
        else if (typeof(T) == typeof(decimal))
        {
            result = FromSystemDecimal(Unsafe.As<T, decimal>(ref value));
        }
        else if (typeof(T) == typeof(Decimal128?))
        {
            return FromNullable(Unsafe.As<T, Decimal128?>(ref value), out result);
        }
        else if (typeof(T) == typeof(decimal?))
        {
            return FromNullable(Unsafe.As<T, decimal?>(ref value), out result);
        }
        else
        {
            result = default;
            return false;
        }

        return true;
#else
        if (typeof(T) == typeof(decimal))
        {
            result = FromSystemDecimal(Unsafe.As<T, decimal>(ref value));
            return true;
        }

        if (typeof(T) == typeof(decimal?))
        {
            return FromNullable(Unsafe.As<T, decimal?>(ref value), out result);
        }

        result = default;
        return false;
#endif
    }

    // The CEL type of System.Decimal and on net11.0 Decimal128, or null for another .NET type.
    private static CelType? DecimalInputType(Type type) =>
#if NET11_0_OR_GREATER
        type == typeof(Decimal128) || type == typeof(decimal) ? CelType.Decimal : null;
#else
        type == typeof(decimal) ? CelType.Decimal : null;
#endif

#if NET11_0_OR_GREATER
    // The name of a value that is not finite as string(double) writes it: a NaN has all of the bits 122 to 126 set, an infinity has the
    // sign of bit 127.
    private static string NotFiniteName(Decimal128 value)
    {
        ulong high = (ulong)(Decimal128.EncodeBinary(value) >> 64);
        return ((high >> 58) & 0x1F) == 0x1F ? "NaN" : (high >> 63) != 0 ? "-Inf" : "+Inf";
    }

    // Decimal128 as a result: exactly.
    private sealed class Decimal128Row() : ClrRow<Decimal128>(CelType.Decimal, takesNull: false)
    {
        internal override bool TryConvert(CelValue value, CelType type, IEvaluationMeter? meter, [MaybeNull] out Decimal128 result) =>
            value.TryGetDecimal128(out result);

        private protected override bool HoldsCore(CelType type) => type.Kind == CelTypeKind.Decimal;

        private protected override bool CannotFailCore(CelType type) => type.Kind == CelTypeKind.Decimal;
    }
#endif

    // System.Decimal as a result or a parameter: on net11.0 rounded half to even to 28 places and 96 bits, so a value out of the range
    // does not fit and the conversion can fail; on net10.0 the value itself.
    private sealed class SystemDecimalRow() : ClrRow<decimal>(CelType.Decimal, takesNull: false)
    {
        internal override bool TryConvert(CelValue value, CelType type, IEvaluationMeter? meter, [MaybeNull] out decimal result)
        {
            result = 0m;
            return value.TryGetPlatformDecimal(out PlatformDecimal number) && DecimalPlatform.TryToSystemDecimal(number, out result);
        }

        private protected override bool HoldsCore(CelType type) => type.Kind == CelTypeKind.Decimal;

#if NET11_0_OR_GREATER
        private protected override bool CannotFailCore(CelType type) => false;
#else
        private protected override bool CannotFailCore(CelType type) => type.Kind == CelTypeKind.Decimal;
#endif
    }

    private static class DecimalRows
    {
        internal static ClrRow[] Create()
        {
            var systemRow = new SystemDecimalRow();
#if NET11_0_OR_GREATER
            var exactRow = new Decimal128Row();
            return
            [
                exactRow, systemRow,
                new NullableRow<Decimal128>(exactRow, wrapped: false, leaf: false),
                new NullableRow<decimal>(systemRow, wrapped: false, leaf: false),
            ];
#else
            return [systemRow, new NullableRow<decimal>(systemRow, wrapped: false, leaf: false)];
#endif
        }
    }
}

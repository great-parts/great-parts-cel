#if NET11_0_OR_GREATER
using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Values;

// The rows of decimal: Decimal128, System.Decimal and the Nullable<> of each, as input and as result. The CEL type is decimal for all
// of them. A typed collection of decimals is not a row (the closed list of elements has no decimal), as it is not for time.
internal static partial class ConversionRules
{
    // The texts of the error of a decimal that is a NaN or an infinity, by the first part of the error.
    private static readonly Func<CelError, string> NotFiniteText = static error => (string)error.First! + " is out of the range of decimal";

    /// <summary>
    /// The CEL value of a <see cref="Decimal128"/>: a decimal, or for a NaN and an infinity, which CEL has no value for, the error value
    /// <c>ConversionRange</c> <c>NaN is out of the range of decimal</c> that has no place.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>A decimal or the error value.</returns>
    internal static CelValue FromDecimal128(Decimal128 value) =>
        Decimal128.IsFinite(value) ? CelValue.FromDecimal(value) : CelValue.FromError(new CelError(CelErrorCode.ConversionRange, NotFiniteText, null, 0, NotFiniteName(value)));

    /// <summary>
    /// The decimal of a <see cref="decimal"/>, exactly, with its scale as the exponent and the sign of its zero.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The decimal.</returns>
    internal static CelValue FromSystemDecimal(decimal value) =>
        CelValue.FromDecimal(DecimalNumberRules.FromSystemDecimal(value).ToDecimal128());

    // A boxed decimal, a Decimal128 or a System.Decimal, in a place of the type dyn; null for a value of another type.
    private static CelValue? FromBoxedDecimal(object? value) => value switch
    {
        Decimal128 number => FromDecimal128(number),
        decimal number => CelValue.FromSystemDecimalOrigin(DecimalNumberRules.FromSystemDecimal(number).ToDecimal128()),
        _ => null,
    };

    /// <summary>
    /// Tells whether the conversion of a binding of the static type <typeparamref name="T"/> met a NaN or an infinity of a
    /// <see cref="Decimal128"/>, which <c>CelBindings.Add</c> refuses.
    /// </summary>
    /// <typeparam name="T">The static type of the binding.</typeparam>
    /// <param name="converted">The result of the conversion.</param>
    /// <returns><see langword="true"/> for an error value of <c>Decimal128</c> or <c>Decimal128?</c>.</returns>
    internal static bool IsNotFiniteDecimal<T>(CelValue converted) =>
        converted.Kind == CelValueKind.Error && (typeof(T) == typeof(Decimal128) || typeof(T) == typeof(Decimal128?));

    // Decimal128, decimal and their Nullable<> by their static type.
    private static bool TryFromDecimal<T>(T value, out CelValue result)
    {
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
    }

    // The CEL type of Decimal128 and System.Decimal, or null for another .NET type.
    private static CelType? DecimalInputType(Type type) =>
        type == typeof(Decimal128) || type == typeof(decimal) ? CelType.Decimal : null;

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
            value.TryGetDecimal(out result);

        private protected override bool HoldsCore(CelType type) => type.Kind == CelTypeKind.Decimal;

        private protected override bool CannotFailCore(CelType type) => type.Kind == CelTypeKind.Decimal;
    }

    // System.Decimal as a result or a parameter: rounded half to even to 28 places and 96 bits; a value out of the range does not
    // fit, so the conversion can fail.
    private sealed class SystemDecimalRow() : ClrRow<decimal>(CelType.Decimal, takesNull: false)
    {
        internal override bool TryConvert(CelValue value, CelType type, IEvaluationMeter? meter, [MaybeNull] out decimal result)
        {
            result = 0m;
            return value.TryGetDecimal(out Decimal128 number) && DecimalNumberRules.ToSystemDecimal(DecimalParts.From(number), out result);
        }

        private protected override bool HoldsCore(CelType type) => type.Kind == CelTypeKind.Decimal;

        private protected override bool CannotFailCore(CelType type) => false;
    }

    private static class DecimalRows
    {
        internal static ClrRow[] Create()
        {
            var exactRow = new Decimal128Row();
            var systemRow = new SystemDecimalRow();
            return [exactRow, systemRow, new NullableTimeRow<Decimal128>(exactRow), new NullableTimeRow<decimal>(systemRow)];
        }
    }
}
#endif

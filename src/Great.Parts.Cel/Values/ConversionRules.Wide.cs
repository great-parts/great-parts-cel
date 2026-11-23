using System;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Values;

// The rows of char, Int128, UInt128 and BigInteger and the Nullable<> of each as a result, a parameter, an element, a value or a key.
// The conversions to CEL are in WideRules. Unlike time and decimal, these rows are leaves: lists, optionals and dictionaries of them
// are rows too, and char is a key.
internal static partial class ConversionRules
{
    // Reads a CEL value as a .NET type of a leaf row; false when the value is of another kind or does not fit.
    private delegate bool TryReadLeaf<T>(CelValue value, out T result);

    // The row of a leaf type that a reader converts: char, Int128, UInt128, BigInteger, DateOnly and TimeOnly. char narrows: a string of
    // another length does not fit. An int always fits Int128 and BigInteger, and a uint UInt128, so those rows cannot fail, nor can the
    // rows of a date and a time of day.
    private sealed class ReadRow<T>(CelType expectedType, bool narrowing, TryReadLeaf<T> read) : LeafRow<T>(expectedType, takesNull: false)
    {
        internal override bool TryConvert(CelValue value, CelType type, IEvaluationMeter? meter, [MaybeNull] out T result)
        {
            bool converted = read(value, out T item);
            result = item;
            return converted;
        }

        private protected override bool HoldsCore(CelType type) => type.Kind == ExpectedType.Kind;

        private protected override bool CannotFailCore(CelType type) => !narrowing && type.Kind == ExpectedType.Kind;
    }

    private static class WideRows
    {
        internal static ClrRow[] Create(out ClrRow<char> charRow)
        {
            var characters = new ReadRow<char>(CelType.String, narrowing: true, TryReadChar);
            var signed = new ReadRow<Int128>(CelType.Int, narrowing: false, TryReadInt128);
            var unsigned = new ReadRow<UInt128>(CelType.UInt, narrowing: false, TryReadUInt128);
            var big = new ReadRow<BigInteger>(CelType.Int, narrowing: false, TryReadBigInteger);
            charRow = characters;
            return
            [
                characters, signed, unsigned, big,
                new NullableRow<char>(characters), new NullableRow<Int128>(signed), new NullableRow<UInt128>(unsigned), new NullableRow<BigInteger>(big),
            ];
        }

        private static bool TryReadChar(CelValue value, out char result)
        {
            result = default;
            if (value.TryGetString(out string? text) && text.Length == 1 && !char.IsSurrogate(text[0]))
            {
                result = text[0];
                return true;
            }

            return false;
        }

        private static bool TryReadInt128(CelValue value, out Int128 result)
        {
            bool converted = value.TryGetInt64(out long number);
            result = number;
            return converted;
        }

        private static bool TryReadUInt128(CelValue value, out UInt128 result)
        {
            bool converted = value.TryGetUInt64(out ulong number);
            result = number;
            return converted;
        }

        private static bool TryReadBigInteger(CelValue value, out BigInteger result)
        {
            bool converted = value.TryGetInt64(out long number);
            result = number;
            return converted;
        }
    }
}

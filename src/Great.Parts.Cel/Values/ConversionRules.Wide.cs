using System;
using System.Numerics;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Values;

// The rows of char, Int128, UInt128 and BigInteger and the Nullable<> of each as a result, a parameter, an element, a value or a key.
// The conversions to CEL are in WideRules. Unlike time and decimal, these rows are leaves: lists, optionals and dictionaries of them
// are rows too, and char is a key.
internal static partial class ConversionRules
{
    private static class WideRows
    {
        internal static ClrRow[] Create(out ClrRow<char> charRow)
        {
            var characters = new LeafReadRow<char>(CelType.String, narrowing: true, TryReadChar);
            var signed = new LeafReadRow<Int128>(CelType.Int, narrowing: false, TryReadInt128);
            var unsigned = new LeafReadRow<UInt128>(CelType.UInt, narrowing: false, TryReadUInt128);
            var big = new LeafReadRow<BigInteger>(CelType.Int, narrowing: false, TryReadBigInteger);
            charRow = characters;
            return
            [
                characters, signed, unsigned, big,
                new LeafNullableRow<char>(characters), new LeafNullableRow<Int128>(signed),
                new LeafNullableRow<UInt128>(unsigned), new LeafNullableRow<BigInteger>(big),
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

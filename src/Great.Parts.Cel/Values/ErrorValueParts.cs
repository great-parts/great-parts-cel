using System;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel.Values;

// An error of evaluation whose message names a value keeps the value in its parts without a copy and without
// boxing: the kind in the byte, a bool, an int, a uint or the bits of a double in the number, a string, a list or a
// map by reference in the second part. The format reads the value back when the message is read.
internal static class ErrorValueParts
{
    // An error of evaluation with the first part and the value; without a line table it has no position.
    internal static CelError Create(CelErrorCode code, Func<CelError, string> format, LineTable? lines, int offset, object? first, CelValue value)
    {
        object? reference = null;
        long number = 0;
        if (value.TryGetInt64(out long signed))
        {
            number = signed;
        }
        else if (value.TryGetUInt64(out ulong unsigned))
        {
            number = (long)unsigned;
        }
        else if (value.TryGetDouble(out double real))
        {
            number = BitConverter.DoubleToInt64Bits(real);
        }
        else if (value.TryGetBoolean(out bool flag))
        {
            number = flag ? 1 : 0;
        }
        else if (value.TryGetString(out string? text))
        {
            reference = text;
        }
        else if (value.TryGetList(out CelList? list))
        {
            reference = list;
        }
        else if (value.TryGetMap(out CelMap? map))
        {
            reference = map;
        }

        return new CelError(code, format, lines, offset, first, reference, number, (byte)value.Kind);
    }

    // The kind of the value of an error that Create made.
    internal static CelValueKind KindOf(CelError error) => (CelValueKind)error.Small;

    // The value of an error that Create made, for the kinds bool, int, uint, double, string, list and map; for another
    // kind only KindOf is kept.
    internal static CelValue ValueOf(CelError error) => KindOf(error) switch
    {
        CelValueKind.Bool => CelValue.FromBoolean(error.Number != 0),
        CelValueKind.Int => CelValue.FromInt64(error.Number),
        CelValueKind.UInt => CelValue.FromUInt64((ulong)error.Number),
        CelValueKind.Double => CelValue.FromDouble(BitConverter.Int64BitsToDouble(error.Number)),
        CelValueKind.String => CelValue.FromString((string)error.Second!),
        CelValueKind.List => CelValue.FromList((CelList)error.Second!),
        CelValueKind.Map => CelValue.FromMap((CelMap)error.Second!),
        _ => default,
    };
}

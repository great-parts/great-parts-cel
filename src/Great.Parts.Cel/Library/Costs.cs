using System;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library;

// The cost functions of the arguments of the standard library: the part of a cost that the call node
// charges before the implementation runs, beyond the fixed units of the overload. Lengths are UTF-16 code
// units of strings and bytes of bytes; an argument of another kind adds nothing, since the implementation
// rejects it.
internal static class Costs
{
    // ⌈Lₐ/10⌉: an operation that passes over its strings or bytes.
    internal static readonly CelCostFunction Text = static arguments => Tenths(TextLength(arguments));

    // ⌈Lₐ/10⌉ + ⌈Lᵣ/10⌉ with Lᵣ = Lₐ: a concatenation of strings or bytes.
    internal static readonly CelCostFunction Concatenation = static arguments => 2 * Tenths(TextLength(arguments));

    // ⌈L/10⌉ for a string key of length L: a lookup in a map hashes the key and compares it.
    internal static readonly CelCostFunction StringKey = static arguments =>
    {
        ulong length = 0;
        foreach (CelValue argument in arguments)
        {
            if (argument.TryGetString(out string? key))
            {
                length += (ulong)key.Length;
            }
        }

        return Tenths(length);
    };

    // ⌈L/10⌉ for a string key of length L of a map, which a lookup hashes and compares; for a JSON object, which the lookup walks
    // whole, n · (1 + ⌈L/10⌉) for n properties, and nothing for a key that is not a string, which finds nothing without a walk.
    internal static readonly CelCostFunction IndexKey = static arguments =>
    {
        if (arguments.Length != 2 || !arguments[0].TryGetMap(out CelMap? map))
        {
            return StringKey(arguments);
        }

        return arguments[1].TryGetString(out string? key) ? map.ProbeUnits(Tenths((ulong)key.Length)) : 0;
    };

    // n for an index of a JSON array of n elements, instead of the unit of the index of a list.
    internal static readonly CelCostFunction IndexList = static arguments =>
        arguments.Length > 0 && arguments[0].TryGetList(out CelList? list) ? list.IndexUnits : 0;

    // n, the size of the result of a concatenation of lists.
    internal static readonly CelCostFunction ListSizes = static arguments =>
    {
        ulong size = 0;
        foreach (CelValue argument in arguments)
        {
            if (argument.TryGetList(out CelList? list))
            {
                size += (ulong)list.Count;
            }
        }

        return size;
    };

    // ⌈x/10⌉.
    internal static ulong Tenths(ulong length) => (length + 9) / 10;

    private static ulong TextLength(ReadOnlySpan<CelValue> arguments)
    {
        ulong length = 0;
        foreach (CelValue argument in arguments)
        {
            if (argument.TryGetString(out string? text))
            {
                length += (ulong)text.Length;
            }
            else if (argument.TryGetBytes(out ReadOnlyMemory<byte> bytes))
            {
                length += (ulong)bytes.Length;
            }
        }

        return length;
    }
}

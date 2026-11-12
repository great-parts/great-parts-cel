#if NET11_0_OR_GREATER
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library.Decimals;

// The cost functions of the set decimal: the part of a cost that the call node charges before the implementation runs, beyond the
// fixed unit of the overload. Library.Decimals cannot reference Library, so the few lines of Costs.Text are repeated here. The charge
// of a text that a call makes while it runs, ⌈Lᵣ/10⌉ for the string of decimal_to_string, is made by the implementation itself.
internal static class DecimalCosts
{
    // ⌈Lₐ/10⌉ of the length of the string arguments: the string that decimal(string) reads. An argument of another kind adds nothing,
    // since the implementation rejects it.
    internal static readonly CelCostFunction Text = static arguments =>
    {
        ulong length = 0;
        foreach (CelValue argument in arguments)
        {
            if (argument.TryGetString(out string? text))
            {
                length += (ulong)text.Length;
            }
        }

        return Tenths(length);
    };

    // ⌈x/10⌉.
    internal static ulong Tenths(ulong length) => (length + 9) / 10;
}
#endif

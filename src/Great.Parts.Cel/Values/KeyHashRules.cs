using System;

namespace Great.Parts.Cel.Values;

/// <summary>
/// Which kinds can be map keys, and the hash of keys, consistent with <see cref="EqualityRules"/>: an
/// <c>int</c>, a <c>uint</c> and a <c>double</c> with the same integral value have the same hash. The hash of an
/// integral value, as that of a string, has a random seed for the process, so that keys chosen to collide do not
/// make a lookup linear.
/// </summary>
internal static class KeyHashRules
{
    /// <summary>
    /// Tells whether a value of <paramref name="kind"/> can be a map key: <c>int</c>, <c>uint</c>,
    /// <c>bool</c> and <c>string</c>.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns><see langword="true"/> for the kinds of map keys.</returns>
    internal static bool IsKeyKind(CelValueKind kind) =>
        kind is CelValueKind.Int or CelValueKind.UInt or CelValueKind.Bool or CelValueKind.String;

    /// <summary>
    /// Computes the hash of a key, or of a <c>double</c> that looks a key up.
    /// </summary>
    /// <param name="value">The key or the probe.</param>
    /// <param name="hash">The hash when the method returns <see langword="true"/>; otherwise 0.</param>
    /// <returns>
    /// <see langword="true"/> for a value of a key kind and for a <c>double</c> with an integral value inside the
    /// open range of <c>int</c> or in the range of <c>uint</c>; <see langword="false"/> for any other value, which equals no key.
    /// </returns>
    internal static bool TryGetHash(CelValue value, out int hash)
    {
        hash = 0;
        if (value.TryGetInt64(out long intValue))
        {
            hash = IntegerHash(unchecked((ulong)intValue));
        }
        else if (value.TryGetUInt64(out ulong uintValue))
        {
            hash = IntegerHash(uintValue);
        }
        else if (value.TryGetDouble(out double number))
        {
            if (EqualityRules.IsLosslessInt(number))
            {
                hash = IntegerHash(unchecked((ulong)(long)number));
            }
            else if (EqualityRules.IsLosslessUInt(number))
            {
                hash = IntegerHash((ulong)number);
            }
            else
            {
                return false;
            }
        }
        else if (value.TryGetBoolean(out bool boolValue))
        {
            hash = boolValue ? 1 : 0;
        }
        else if (value.TryGetString(out string? text))
        {
            hash = text.GetHashCode(StringComparison.Ordinal);
        }
        else
        {
            return false;
        }

        return true;
    }

    // One hash for an integral value, whatever the kind that holds it: the bits of a negative int are its two's complement.
    // HashCode mixes them with the random seed of the process.
    private static int IntegerHash(ulong bits) => HashCode.Combine(bits);
}

using System;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library.Extensions.Strings;

// The writer of the text of one element of a list or of a map: the part of json.encode and of format that differs.
internal interface IElementText
{
    // Appends the text of the value, or gives the error value of the call.
    static abstract bool TryAppend(ref ResultBuilder builder, CelValue value, in CallSite site, out CelValue error);
}

// The steps that json.encode and the clause %s of format take alike when they write a time, a list or a map: the texts of the elements
// and the order of the pairs of a map are their own.
internal static class ContainerText
{
    // The text of a timestamp, as string() writes it, between double quotes when quoted.
    internal static void AppendTimestamp(ref ResultBuilder builder, CelTimestamp timestamp, bool quoted)
    {
        Span<char> text = stackalloc char[TimeTextRules.MaxTimestampLength + 2];
        int open = quoted ? 1 : 0;
        int written = TimeTextRules.WriteTimestamp(timestamp, text[open..]);
        Quote(text, open, written);
        builder.Append(text[..(written + (2 * open))]);
    }

    // The text of a duration, as string() writes it, between double quotes when quoted.
    internal static void AppendDuration(ref ResultBuilder builder, CelDuration duration, bool quoted)
    {
        Span<char> text = stackalloc char[TimeTextRules.MaxDurationLength + 2];
        int open = quoted ? 1 : 0;
        int written = TimeTextRules.WriteDuration(duration, text[open..]);
        Quote(text, open, written);
        builder.Append(text[..(written + (2 * open))]);
    }

    // [ the elements separated by the separator ]
    internal static bool TryAppendList<TElement>(
        ref ResultBuilder builder,
        CelList list,
        string separator,
        string stackMessage,
        in CallSite site,
        out CelValue error)
        where TElement : IElementText
    {
        error = default;
        if (!System.Runtime.CompilerServices.RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            error = site.Error(CelErrorCode.InsufficientStack, stackMessage);
            return false;
        }

        builder.Append("[");
        int count = list.Count;
        for (int i = 0; i < count; i++)
        {
            if (i > 0)
            {
                builder.Append(separator);
            }

            if (!TElement.TryAppend(ref builder, list.Read(i, site.Meter), in site, out error))
            {
                return false;
            }
        }

        builder.Append("]");
        return true;
    }

    // The steps before the pairs of a map are written: the stack, the error of a key that the map repeats, the empty map, which is
    // written whole with a count of 0, and the charge of the walk. False with the error value of the call when a step fails.
    internal static bool TryBeginMap(ref ResultBuilder builder, CelMap map, string stackMessage, in CallSite site, out int count, out CelValue error)
    {
        count = 0;
        error = default;
        if (!System.Runtime.CompilerServices.RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            error = site.Error(CelErrorCode.InsufficientStack, stackMessage);
            return false;
        }

        if (map.TryGetKeyError(site.Meter, out CelValue keyError))
        {
            error = site.Placed(keyError);
            return false;
        }

        count = map.Count;
        if (count == 0)
        {
            builder.Append("{}");
            return true;
        }

        if (site.Meter is { } meter)
        {
            map.ChargeEnumeration(meter);
        }

        return true;
    }

    // The double quotes around a text of the given length that was written after room for the opening one, when there is room (open is 1).
    private static void Quote(Span<char> text, int open, int written)
    {
        if (open != 0)
        {
            text[0] = '"';
            text[written + 1] = '"';
        }
    }
}

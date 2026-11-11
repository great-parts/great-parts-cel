using System;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library.Extensions.Strings;

// The function format of the set strings, version 5: the clauses of the format string filled with the elements of a list. The rules
// are in FormatParser and FormatValues, which the check at compilation shares.
internal static partial class StringFunctions
{
    // string_format, string.format(list(dyn)) -> string: the parser takes the format string piece by piece, each piece of text and
    // each clause is charged as it is appended, and the first failure is the error of the call. Elements of the list past the last
    // clause are not used.
    internal static CelValue Format(ReadOnlySpan<CelValue> arguments, in CallSite site)
    {
        if (!arguments[0].TryGetString(out string? format) || !arguments[1].TryGetList(out CelList? list))
        {
            return site.NoMatchingOverload(arguments);
        }

        var builder = new ResultBuilder(site.Meter);
        try
        {
            return TryFormat(ref builder, format, list, in site, out CelValue error) ? CelValue.FromString(builder.ToResultString()) : error;
        }
        finally
        {
            builder.Dispose();
        }
    }

    // The argument cost of format: n for the size of the list and ⌈Lₐ/10⌉ for the length of the format string alone.
    internal static ulong FormatArgumentCost(ReadOnlySpan<CelValue> arguments)
    {
        ulong units = 0;
        foreach (CelValue argument in arguments)
        {
            if (argument.TryGetList(out CelList? list))
            {
                units += (ulong)list.Count;
            }
            else if (argument.TryGetString(out string? format))
            {
                units += Costs.Tenths((ulong)format.Length);
            }
        }

        return units;
    }

    private static bool TryFormat(ref ResultBuilder builder, string format, CelList list, in CallSite site, out CelValue error)
    {
        error = default;
        var parser = new FormatParser(format, list.Count);
        while (true)
        {
            switch (parser.Next())
            {
                case FormatStep.End:
                    return true;
                case FormatStep.Text:
                    builder.Append(parser.Text);
                    break;
                case FormatStep.Failure:
                    error = FormatValues.FormatError(parser.Failure, in site);
                    return false;
                default:
                    CelValue element = list.Read(parser.Clause.Index, site.Meter);
                    if (element.Kind == CelValueKind.Error)
                    {
                        error = site.Placed(element);
                        return false;
                    }

                    if (!FormatValues.TryAppend(ref builder, parser.Clause, element, in site, out error))
                    {
                        return false;
                    }

                    break;
            }
        }
    }
}

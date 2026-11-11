namespace Great.Parts.Cel.Library;

// The fixed limits of the functions of the extension sets. None is configurable, and each belongs to the version of its set.
internal static class ExtensionLimits
{
    // The largest precision of a clause of format, in the set strings from version 5 on.
    internal const int MaxFormatPrecision = 100;

    // The largest argument of lists.range, in the set lists from version 4 on.
    internal const int MaxRangeSize = 1_000_000;
}

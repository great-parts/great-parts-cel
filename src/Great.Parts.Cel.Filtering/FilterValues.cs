using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Filtering;

/// <summary>
/// The CEL value of the value of a restriction, or why the value does not fit the type of its field.
/// </summary>
/// <param name="Value">The constant; <see langword="null"/> for a call and when the value does not fit.</param>
/// <param name="Function">
/// The function that converts the text to the value - <c>timestamp</c>, <c>duration</c> or <c>decimal</c> -, the argument of
/// which is the string <paramref name="Text"/>; <see langword="null"/> for a constant and when the value does not fit.
/// </param>
/// <param name="Text">The argument of <paramref name="Function"/>.</param>
/// <param name="Problem">What is wrong with the value; <see langword="null"/> with a value.</param>
internal readonly record struct FilterValue(CelValue? Value, string? Function, string? Text, string? Problem);

/// <summary>
/// The kinds of a string value of a filter with stars: <c>"*x"</c>, <c>"x*"</c>, <c>"*x*"</c> and <c>"*"</c>.
/// </summary>
internal enum FilterWildcardKind
{
    /// <summary>No star that stands for any text: the string is the value.</summary>
    None,

    /// <summary>A lone star: the field is set.</summary>
    Presence,

    /// <summary>A star at the end: the field starts with the text.</summary>
    StartsWith,

    /// <summary>A star at the start: the field ends with the text.</summary>
    EndsWith,

    /// <summary>A star at both ends: the field contains the text.</summary>
    Contains,
}

/// <summary>
/// A string value of a filter read for its stars.
/// </summary>
/// <param name="Kind">The kind of the pattern.</param>
/// <param name="Core">The text between the stars.</param>
internal readonly record struct FilterWildcard(FilterWildcardKind Kind, string Core);

/// <summary>
/// Reads the value on the right of a comparison by the CEL type of the field on the left: a number by the rules of its type, a
/// text as a string, and, for a field of no known type, by the form of the text.
/// </summary>
internal static class FilterValues
{
    /// <summary>
    /// Reads a value.
    /// </summary>
    /// <param name="type">The type of the field without a wrapper; <see langword="null"/> when the environment does not describe it.</param>
    /// <param name="member">The value as written: a text or a string.</param>
    /// <param name="catalog">The description of the environment.</param>
    /// <param name="enumType">The full name of the enumeration type of the field; <see langword="null"/> when the field is not mapped.</param>
    /// <returns>The value, or what is wrong with it.</returns>
    internal static FilterValue Read(CelType? type, FilterMember member, FieldCatalog catalog, string? enumType)
    {
        FilterString? quoted = member.Segments.Count == 1 ? member.Segments[0].Quoted : null;
        return Read(type, quoted is null ? member.Source : quoted.Value, quoted is null, catalog, enumType);
    }

    /// <summary>
    /// Reads a value written as a text.
    /// </summary>
    /// <param name="type">The type of the field without a wrapper; <see langword="null"/> when the environment does not describe it.</param>
    /// <param name="text">The text, or the value of the string.</param>
    /// <param name="isText">Whether the value was written without quotes.</param>
    /// <param name="catalog">The description of the environment.</param>
    /// <param name="enumType">The full name of the enumeration type of the field; <see langword="null"/> when the field is not mapped.</param>
    /// <returns>The value, or what is wrong with it.</returns>
    internal static FilterValue Read(CelType? type, string text, bool isText, FieldCatalog catalog, string? enumType)
    {
        CelTypeKind? kind = type?.Kind;
        return kind switch
        {
            CelTypeKind.String => Ok(CelValue.FromString(text)),
            CelTypeKind.Int => enumType is null ? Integer(text, catalog) : Enumeration(text, catalog.EnumTypeNamed(enumType)),
            CelTypeKind.UInt => Unsigned(text),
            CelTypeKind.Double => Number(text),
            CelTypeKind.Decimal => IsNumber(text) ? Conversion("decimal", text) : Fail("'" + text + "' is not a number"),
            CelTypeKind.Bool => Boolean(text, isText),
            CelTypeKind.Bytes => isText ? Fail("bytes are written in quotes") : Ok(CelValue.FromBytes(Encoding.UTF8.GetBytes(text))),
            CelTypeKind.Timestamp => Timestamp(text, isText),
            CelTypeKind.Duration => CelDuration.TryParse(text, out _) ? Conversion("duration", text) : Fail("'" + text + "' is not a duration such as 20s or 1h30m"),
            CelTypeKind.Dyn or CelTypeKind.Any or null => ByForm(text, isText),
            _ => Fail("a value of the type " + type + " cannot be compared"),
        };
    }

    /// <summary>
    /// Reads the stars of a string value: a star at the start or at the end that was not written <c>\*</c> stands for any text.
    /// </summary>
    /// <param name="member">The value as written.</param>
    /// <returns>The kind of the pattern and the text between the stars; a value that is not a string, and a string without such stars, is <see cref="FilterWildcardKind.None"/>.</returns>
    internal static FilterWildcard Wildcard(FilterMember member)
    {
        if (member.Segments.Count != 1 || member.Segments[0].Quoted is not { } quoted)
        {
            return new FilterWildcard(FilterWildcardKind.None, member.Source);
        }

        string text = quoted.Value;
        bool leading = text.Length > 0 && text[0] == '*' && Array.IndexOf(quoted.EscapedStars, 0) < 0;
        bool trailing = text.Length > 0 && text[^1] == '*' && Array.IndexOf(quoted.EscapedStars, text.Length - 1) < 0;
        return (leading, trailing) switch
        {
            (true, true) when text.Length == 1 => new FilterWildcard(FilterWildcardKind.Presence, string.Empty),
            (true, true) => new FilterWildcard(FilterWildcardKind.Contains, text[1..^1]),
            (true, false) => new FilterWildcard(FilterWildcardKind.EndsWith, text[1..]),
            (false, true) => new FilterWildcard(FilterWildcardKind.StartsWith, text[..^1]),
            _ => new FilterWildcard(FilterWildcardKind.None, text),
        };
    }

    private static FilterValue Ok(CelValue value) => new(value, null, null, null);

    private static FilterValue Fail(string problem) => new(null, null, null, problem);

    private static FilterValue Conversion(string function, string text) => new(null, function, text, null);

    private static FilterValue Integer(string text, FieldCatalog catalog) =>
        long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long number)
            ? Ok(CelValue.FromInt64(number))
            : catalog.TryGetConstant(text, out long constant)
                ? Ok(CelValue.FromInt64(constant))
                : Fail("'" + text + "' is not an integer that fits 64 bits or the full name of an enumeration constant");

    // The name of a constant, the full name of a constant, or an integer.
    private static FilterValue Enumeration(string text, CelEnumType? type)
    {
        if (type is null)
        {
            return Fail("the enumeration type is not known");
        }

        foreach (KeyValuePair<string, long> constant in type.Constants)
        {
            if (IsNameOf(text, type.Name, constant.Key))
            {
                return Ok(CelValue.FromInt64(constant.Value));
            }
        }

        return long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long number)
            ? Ok(CelValue.FromInt64(number))
            : Fail("'" + text + "' is not a constant of " + type.Name + "; the constants are " + ConstantNames(type));
    }

    // Whether the text is the name of the constant, or the full name: the name of the type, a dot and the name.
    private static bool IsNameOf(string text, string typeName, string constant)
    {
        if (string.Equals(text, constant, StringComparison.Ordinal))
        {
            return true;
        }

        return text.Length == typeName.Length + 1 + constant.Length
            && text.AsSpan(0, typeName.Length).SequenceEqual(typeName)
            && text[typeName.Length] == '.'
            && text.AsSpan(typeName.Length + 1).SequenceEqual(constant);
    }

    private static string ConstantNames(CelEnumType type)
    {
        StringBuilder names = new();
        foreach (KeyValuePair<string, long> constant in type.Constants)
        {
            names.Append(names.Length == 0 ? string.Empty : ", ").Append(constant.Key);
        }

        return names.ToString();
    }

    private static FilterValue Unsigned(string text) =>
        ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out ulong number)
            ? Ok(CelValue.FromUInt64(number))
            : Fail("'" + text + "' is not an integer from 0 to 18446744073709551615");

    private static FilterValue Number(string text) =>
        IsNumber(text) && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) && double.IsFinite(number)
            ? Ok(CelValue.FromDouble(number))
            : Fail("'" + text + "' is not a number");

    private static FilterValue Boolean(string text, bool isText) =>
        isText && text == "true" ? Ok(CelValue.FromBoolean(true))
        : isText && text == "false" ? Ok(CelValue.FromBoolean(false))
        : Fail("'" + text + "' is not true or false");

    // A time is written in quotes: a time with hours contains a colon, which a text does not.
    private static FilterValue Timestamp(string text, bool isText)
    {
        if (isText)
        {
            return Fail("a timestamp is written in quotes, as an RFC 3339 string such as \"2026-01-01T00:00:00Z\"");
        }

        return CelTimestamp.TryParse(text, out _)
            ? Conversion("timestamp", text)
            : Fail("'" + text + "' is not an RFC 3339 timestamp such as 2026-01-01T00:00:00Z");
    }

    // The value of a field of no known type: true and false, an integer, a number, else a string; a string in quotes is a string.
    private static FilterValue ByForm(string text, bool isText)
    {
        if (!isText)
        {
            return Ok(CelValue.FromString(text));
        }

        return text switch
        {
            "true" => Ok(CelValue.FromBoolean(true)),
            "false" => Ok(CelValue.FromBoolean(false)),
            _ when IsInteger(text) && long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long integer) =>
                Ok(CelValue.FromInt64(integer)),
            _ when IsNumber(text) && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) && double.IsFinite(number) =>
                Ok(CelValue.FromDouble(number)),
            _ => Ok(CelValue.FromString(text)),
        };
    }

    // An optional sign and digits: the integer form of a number.
    private static bool IsInteger(string text)
    {
        int digits = SkipSign(text);
        return SkipDigits(text, ref digits) && digits == text.Length;
    }

    // [+-]?\d+(\.\d+)?([eE][+-]?\d+)?, in ASCII: the number of a filter.
    private static bool IsNumber(string text)
    {
        int i = SkipSign(text);
        if (!SkipDigits(text, ref i))
        {
            return false;
        }

        if (i < text.Length && text[i] == '.')
        {
            i++;
            if (!SkipDigits(text, ref i))
            {
                return false;
            }
        }

        if (i < text.Length && text[i] is 'e' or 'E')
        {
            i++;
            i += SkipSign(text, i);
            if (!SkipDigits(text, ref i))
            {
                return false;
            }
        }

        return i == text.Length;
    }

    private static int SkipSign(string text, int index = 0) => index < text.Length && text[index] is '+' or '-' ? 1 : 0;

    // Moves past the ASCII digits; false when there was none.
    private static bool SkipDigits(string text, ref int index)
    {
        int start = index;
        while (index < text.Length && char.IsAsciiDigit(text[index]))
        {
            index++;
        }

        return index > start;
    }
}

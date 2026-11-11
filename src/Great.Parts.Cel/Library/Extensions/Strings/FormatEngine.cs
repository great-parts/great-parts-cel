using System;
using System.Globalization;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Library.Extensions.Strings;

// The letter of a clause of a format string.
internal enum FormatSpecifier : byte
{
    String,
    Decimal,
    Fixed,
    Scientific,
    Binary,
    Octal,
    Hex,
    HexUpper,
}

// Why a clause cannot be used: the reason in the format string, or the kind of a value that its letter does not take. The letter of
// a kind failure is the one that the name says.
internal enum FormatFailureKind : byte
{
    UnexpectedEnd,
    IndexOutOfRange,
    PrecisionUnterminated,
    PrecisionEmpty,
    PrecisionOutOfRange,
    PrecisionTooLarge,
    UnknownClause,
    StringKind,
    DecimalKind,
    FixedKind,
    ScientificKind,
    BinaryKind,
    OctalKind,
    HexKind,
}

// A failure with what its text needs: a number — the index of the clause, the precision or the code point of the letter — and, for a
// kind failure, the name of the type that was given. The name is not read at evaluation, so the error value takes it by reference.
internal readonly record struct FormatFailure(FormatFailureKind Kind, long Number, string? TypeName);

// A clause: its letter, its precision (6 when none is written) and the number of the clause from 0, which is the index of the
// element of the list that it takes.
internal readonly record struct FormatClause(FormatSpecifier Specifier, int Precision, int Index);

// What the parser found next in the format string.
internal enum FormatStep
{
    End,
    Text,
    Clause,
    Failure,
}

// The kinds of value that the clauses tell apart. A static type and a runtime value map to the same kinds, so the table of letters and
// kinds is one for the check at compilation and for evaluation.
internal enum FormatKind : byte
{
    Other,
    Null,
    Bool,
    Int,
    UInt,
    Double,
    String,
    Bytes,
    List,
    Map,
    Type,
    Timestamp,
    Duration,
}

// The parser of a format string, shared by evaluation and the check of a call at compilation. It reads the string from left to
// right, in UTF-16 units: the characters that matter — %, the digits and the letters — are ASCII, so no other unit can be one of
// them. Each call of Next gives the next piece of text, the next clause or the failure that ends the call; after a failure it gives
// End. The first error ends the call, in this order for a clause: no unit after the %, no element for the clause, the precision,
// the letter.
internal struct FormatParser
{
    private const int DefaultPrecision = 6;

    private readonly string _format;
    private readonly int _elementCount;
    private int _position;
    private int _clauses;
    private int _textStart;
    private int _textLength;

    internal FormatParser(string format, int elementCount)
    {
        _format = format;
        _elementCount = elementCount;
        _position = 0;
        _clauses = 0;
        _textStart = 0;
        _textLength = 0;
        Clause = default;
        Failure = default;
    }

    // The text after a Text step: a piece of the format string, or the single % that %% stands for.
    internal readonly ReadOnlySpan<char> Text => _format.AsSpan(_textStart, _textLength);

    // The clause after a Clause step.
    internal FormatClause Clause { get; private set; }

    // The failure after a Failure step.
    internal FormatFailure Failure { get; private set; }

    // The number of clauses that the steps so far gave.
    internal readonly int ClauseCount => _clauses;

    internal FormatStep Next()
    {
        if (_position >= _format.Length)
        {
            return FormatStep.End;
        }

        if (_format[_position] != '%')
        {
            int run = _format.AsSpan(_position).IndexOf('%');
            _textStart = _position;
            _textLength = run < 0 ? _format.Length - _position : run;
            _position += _textLength;
            return FormatStep.Text;
        }

        if (_position + 1 < _format.Length && _format[_position + 1] == '%')
        {
            _textStart = _position;
            _textLength = 1;
            _position += 2;
            return FormatStep.Text;
        }

        return NextClause();
    }

    private FormatStep NextClause()
    {
        int index = _position + 1;
        if (index >= _format.Length)
        {
            return Fail(FormatFailureKind.UnexpectedEnd, 0);
        }

        if (_clauses >= _elementCount)
        {
            return Fail(FormatFailureKind.IndexOutOfRange, _clauses);
        }

        int precision = DefaultPrecision;
        if (_format[index] == '.')
        {
            FormatFailureKind? failure = ReadPrecision(ref index, out precision, out long number);
            if (failure is { } kind)
            {
                return Fail(kind, number);
            }
        }

        FormatSpecifier? specifier = SpecifierOf(_format[index]);
        if (specifier is not { } letter)
        {
            return Fail(FormatFailureKind.UnknownClause, CodePointAt(index));
        }

        Clause = new FormatClause(letter, precision, _clauses++);
        _position = index + 1;
        return FormatStep.Clause;
    }

    // Reads the precision after the point: the digits up to the first unit that is not a digit. A number beyond the range of a long
    // is read to its last digit and is an error of its own; one above the limit names its value.
    private readonly FormatFailureKind? ReadPrecision(ref int index, out int precision, out long number)
    {
        precision = 0;
        number = 0;
        int start = ++index;
        long value = 0;
        bool beyondRange = false;
        while (index < _format.Length && char.IsAsciiDigit(_format[index]))
        {
            int digit = _format[index] - '0';
            if (value > (long.MaxValue - digit) / 10)
            {
                beyondRange = true;
            }
            else
            {
                value = (value * 10) + digit;
            }

            index++;
        }

        if (index >= _format.Length)
        {
            return FormatFailureKind.PrecisionUnterminated;
        }

        if (index == start)
        {
            return FormatFailureKind.PrecisionEmpty;
        }

        if (beyondRange)
        {
            return FormatFailureKind.PrecisionOutOfRange;
        }

        if (value > ExtensionLimits.MaxFormatPrecision)
        {
            number = value;
            return FormatFailureKind.PrecisionTooLarge;
        }

        precision = (int)value;
        return null;
    }

    // The code point at the index: the pair when a valid one starts there, else the unit.
    private readonly long CodePointAt(int index) =>
        char.IsHighSurrogate(_format[index]) && index + 1 < _format.Length && char.IsLowSurrogate(_format[index + 1])
            ? char.ConvertToUtf32(_format[index], _format[index + 1])
            : _format[index];

    private FormatStep Fail(FormatFailureKind kind, long number)
    {
        Failure = new FormatFailure(kind, number, null);
        _position = _format.Length;
        return FormatStep.Failure;
    }

    private static FormatSpecifier? SpecifierOf(char letter) => letter switch
    {
        's' => FormatSpecifier.String,
        'd' => FormatSpecifier.Decimal,
        'f' => FormatSpecifier.Fixed,
        'e' => FormatSpecifier.Scientific,
        'b' => FormatSpecifier.Binary,
        'o' => FormatSpecifier.Octal,
        'x' => FormatSpecifier.Hex,
        'X' => FormatSpecifier.HexUpper,
        _ => null,
    };
}

// The kinds that a letter takes and the texts of the failures.
internal static class FormatRules
{
    private const string ClauseFailed = "could not parse formatting clause: ";
    private const string PrecisionFailed = ClauseFailed + "error while parsing precision: ";
    private const string Formatting = "error during formatting: ";

    // The kind of a runtime value; any kind that no clause takes is Other.
    internal static FormatKind KindOf(CelValueKind kind) => kind switch
    {
        CelValueKind.Null => FormatKind.Null,
        CelValueKind.Bool => FormatKind.Bool,
        CelValueKind.Int => FormatKind.Int,
        CelValueKind.UInt => FormatKind.UInt,
        CelValueKind.Double => FormatKind.Double,
        CelValueKind.String => FormatKind.String,
        CelValueKind.Bytes => FormatKind.Bytes,
        CelValueKind.List => FormatKind.List,
        CelValueKind.Map => FormatKind.Map,
        CelValueKind.Type => FormatKind.Type,
        CelValueKind.Timestamp => FormatKind.Timestamp,
        CelValueKind.Duration => FormatKind.Duration,
        _ => FormatKind.Other,
    };

    // The kind of a static type, which is not dyn: a wrapper is the primitive that it wraps, as the kind of a value that it holds.
    internal static FormatKind KindOf(CelType type)
    {
        CelType unwrapped = type.Kind == CelTypeKind.Wrapper ? type.Parameters[0] : type;
        return unwrapped.Kind switch
        {
            CelTypeKind.Null => FormatKind.Null,
            CelTypeKind.Bool => FormatKind.Bool,
            CelTypeKind.Int => FormatKind.Int,
            CelTypeKind.UInt => FormatKind.UInt,
            CelTypeKind.Double => FormatKind.Double,
            CelTypeKind.String => FormatKind.String,
            CelTypeKind.Bytes => FormatKind.Bytes,
            CelTypeKind.List => FormatKind.List,
            CelTypeKind.Map => FormatKind.Map,
            CelTypeKind.Type => FormatKind.Type,
            CelTypeKind.Timestamp => FormatKind.Timestamp,
            CelTypeKind.Duration => FormatKind.Duration,
            _ => FormatKind.Other,
        };
    }

    // Whether the letter takes a value of the kind.
    internal static bool Accepts(FormatSpecifier specifier, FormatKind kind) => specifier switch
    {
        FormatSpecifier.String => kind != FormatKind.Other,
        FormatSpecifier.Decimal or FormatSpecifier.Fixed or FormatSpecifier.Scientific => kind is FormatKind.Int or FormatKind.UInt or FormatKind.Double,
        FormatSpecifier.Binary => kind is FormatKind.Bool or FormatKind.Int or FormatKind.UInt,
        FormatSpecifier.Octal => kind is FormatKind.Int or FormatKind.UInt,
        _ => kind is FormatKind.Int or FormatKind.UInt or FormatKind.String or FormatKind.Bytes,
    };

    // The failure of a letter given a value that it does not take.
    internal static FormatFailureKind KindFailure(FormatSpecifier specifier) => specifier switch
    {
        FormatSpecifier.String => FormatFailureKind.StringKind,
        FormatSpecifier.Decimal => FormatFailureKind.DecimalKind,
        FormatSpecifier.Fixed => FormatFailureKind.FixedKind,
        FormatSpecifier.Scientific => FormatFailureKind.ScientificKind,
        FormatSpecifier.Binary => FormatFailureKind.BinaryKind,
        FormatSpecifier.Octal => FormatFailureKind.OctalKind,
        _ => FormatFailureKind.HexKind,
    };

    // The detail of an error: the text that follows "invalid format string: " at compilation and "invalid argument to 'format': "
    // at evaluation.
    internal static string Detail(FormatFailureKind kind, long number, string? typeName) => kind switch
    {
        FormatFailureKind.UnexpectedEnd => "unexpected end of string",
        FormatFailureKind.IndexOutOfRange => "index " + number.ToString(CultureInfo.InvariantCulture) + " out of range",
        FormatFailureKind.PrecisionUnterminated => PrecisionFailed + "could not find end of precision specifier",
        FormatFailureKind.PrecisionEmpty => PrecisionFailed + "error while converting precision to integer: invalid syntax",
        FormatFailureKind.PrecisionOutOfRange => PrecisionFailed + "error while converting precision to integer: value out of range",
        FormatFailureKind.PrecisionTooLarge => PrecisionFailed + "precision " + number.ToString(CultureInfo.InvariantCulture)
            + " exceeds maximum allowed precision " + ExtensionLimits.MaxFormatPrecision.ToString(CultureInfo.InvariantCulture),
        FormatFailureKind.UnknownClause => ClauseFailed + "unrecognized formatting clause \"" + CodePointText(number) + "\"",
        _ => Formatting + KindText(kind) + typeName,
    };

    // The detail of a call that gives more elements than the format has clauses; only the check at compilation reports it.
    internal static string TooManyArguments(int expected, int actual) =>
        "too many arguments supplied to string.format (expected " + expected.ToString(CultureInfo.InvariantCulture)
        + ", got " + actual.ToString(CultureInfo.InvariantCulture) + ")";

    private static string CodePointText(long codePoint) =>
        codePoint > char.MaxValue ? char.ConvertFromUtf32((int)codePoint) : ((char)codePoint).ToString();

    private static string KindText(FormatFailureKind kind) => kind switch
    {
        FormatFailureKind.StringKind => "string clause can only be used on strings, bools, bytes, ints, doubles, maps, lists, types, durations, and timestamps, was given ",
        FormatFailureKind.DecimalKind => "decimal clause can only be used on ints, uints, and doubles, was given ",
        FormatFailureKind.FixedKind => "fixed-point clause can only be used on ints, uints, and doubles, was given ",
        FormatFailureKind.ScientificKind => "scientific clause can only be used on ints, uints, and doubles, was given ",
        FormatFailureKind.BinaryKind => "only ints, uints, and bools can be formatted as binary, was given ",
        FormatFailureKind.OctalKind => "octal clause can only be used on ints and uints, was given ",
        _ => "only ints, uints, bytes, and strings can be formatted as hex, was given ",
    };
}

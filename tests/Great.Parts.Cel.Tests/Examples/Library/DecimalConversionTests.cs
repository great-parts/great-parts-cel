#if NET11_0_OR_GREATER
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// The conversions of the set decimal, version 1 (the page of the extension set decimal, "The 18 overloads"; the page of the decimal
// text, "Reading a number", "The text of a value", "From and to double", "Integers"): decimal() of an int, a uint, a double, a string and a
// decimal (int64_to_decimal, uint64_to_decimal, double_to_decimal, string_to_decimal, decimal_to_decimal), and int(), uint(),
// double() and string() of a decimal (decimal_to_int64, decimal_to_uint64, decimal_to_double, decimal_to_string). A row that shows
// the digits and the exponent is written as string(x).
//
// decimal(int) and decimal(uint) are exact. decimal(double) takes the digits of the shortest text that reads back to the double —
// those of string(double) — and lays them out as string(double) does, so decimal(x) and decimal(string(x)) are the same value with
// the same exponent: decimal(100.0) is 100 with the exponent 0, decimal(1e6) is 1 with the exponent 6; a NaN and an infinity are
// ConversionRange (3007), "{value} is out of the range of decimal" with the value as string(double) writes it. decimal(string)
// reads [+|-] (digits [. [digits]] | . digits) [(e|E) [+|-] digits] and nothing else — no space, no separator, no Infinity, no NaN, no
// hexadecimal form: another text is InvalidArgument (3008), "invalid argument to 'decimal': cannot parse <text> as decimal"; more
// than 34 significant digits are rounded to 34, half to even, and an exponent out of -6176 to 6111 after the rounding, in
// a zero too, is ConversionRange (the arithmetic gives a zero for a vanishing result, a text is refused: "1e-6200" is not a zero), the text
// of the error being the string as written; the grammar is checked before the range. decimal(decimal) is the identity: the
// exponent and the sign of a zero are kept. int() and uint() truncate toward zero and then check the range — so uint(-0.5m) is 0u,
// which uint(-0.5) is not — and the value out of the range is ConversionRange with the text of the decimal and the name of the
// type; double() is the nearest double, half to even, ConversionRange when it would be infinite and a zero or a subnormal value
// without an error when it vanishes. Places: the "(" of the call. Rows are those of ExtensionRows.
[Trait("Requirement", "F-5")]
[Trait("Requirement", "F-1")]
public sealed class DecimalConversionTests
{
    private const string Max34 = DecimalExample.Max34;
    private const string Digits35 = "12345678901234567890123456789012345";
    private const string Digits35Odd = "12345678901234567890123456789012355";
    private const string Digits35Above = "12345678901234567890123456789012346";
    private const string Nines35 = "99999999999999999999999999999999999";
    private const string OneAnd39Zeros = "1000000000000000000000000000000000000000";

    private const string FromIntegers = """
        string(decimal(5)) => '5'
        string(decimal(0)) => '0'
        string(decimal(-5)) => '-5'
        string(decimal(-9223372036854775808)) => '-9223372036854775808'
        string(decimal(9223372036854775807)) => '9223372036854775807'
        string(decimal(0u)) => '0'
        string(decimal(5u)) => '5'
        string(decimal(18446744073709551615u)) => '18446744073709551615'
        decimal(5) == 5m => true
        decimal(1u) == 1m => true
        string(decimal(dyn(5))) => '5'
        string(decimal(dyn(5u))) => '5'
        """;

    // The digits of the double are those of string(double): decimal(x) and decimal(string(x)) are one value with one exponent.
    private const string FromDoubles = """
        string(decimal(0.1)) => '0.1'
        string(decimal(100.0)) => '100'
        string(decimal(1e6)) => '1E+6'
        string(decimal(123456.0)) => '123456'
        string(decimal(123456789.0)) => '123456789'
        string(decimal(1.5e10)) => '1.5E+10'
        string(decimal(1e21)) => '1E+21'
        string(decimal(1e-5)) => '0.00001'
        string(decimal(0.001)) => '0.001'
        string(decimal(1.25)) => '1.25'
        string(decimal(-2.5)) => '-2.5'
        string(decimal(0.0)) => '0'
        string(decimal(-0.0)) => '-0'
        string(decimal(dyn(1.5))) => '1.5'
        decimal(0.1) == decimal(string(0.1)) => true
        decimal(0.1) + decimal(0.2) == decimal(0.3) => true
        string(decimal(0.1)) == string(decimal('0.1')) => true
        string(decimal(1e6)) == string(decimal(string(1e6))) => true
        string(decimal(100.0)) == string(decimal(string(100.0))) => true
        string(decimal(1.5e10)) == string(decimal(string(1.5e10))) => true
        string(decimal(1e-5)) == string(decimal(string(1e-5))) => true
        """;

    private const string NotFinite = """
        Evaluate | decimal^(0.0 / 0.0) | ConversionRange | NaN is out of the range of decimal
        Evaluate | decimal^(1.0 / 0.0) | ConversionRange | +Inf is out of the range of decimal
        Evaluate | decimal^(-1.0 / 0.0) | ConversionRange | -Inf is out of the range of decimal
        """;

    private const string FromStrings = $$"""
        string(decimal('1.50')) => '1.50'
        string(decimal('0.1')) => '0.1'
        string(decimal('-0')) => '-0'
        string(decimal('-0.0')) => '-0.0'
        string(decimal('0.000')) => '0.000'
        string(decimal('+1.5')) => '1.5'
        string(decimal('1e3')) => '1E+3'
        string(decimal('1E+3')) => '1E+3'
        string(decimal('.5')) => '0.5'
        string(decimal('5.')) => '5'
        string(decimal('100')) => '100'
        string(decimal('00012')) => '12'
        string(decimal('1e-6176')) => '1E-6176'
        string(decimal('1e6111')) => '1E+6111'
        string(decimal('1234567890123456789012345678901234')) => '1234567890123456789012345678901234'
        string(decimal('{{Digits35}}')) => '1.234567890123456789012345678901234E+34'
        string(decimal('{{Digits35Odd}}')) => '1.234567890123456789012345678901236E+34'
        string(decimal('{{Digits35Above}}')) => '1.234567890123456789012345678901235E+34'
        string(decimal('{{Nines35}}')) => '1.000000000000000000000000000000000E+35'
        string(decimal('{{OneAnd39Zeros}}')) => '1.000000000000000000000000000000000E+39'
        string(decimal('{{Max34}}e6111')) => '9.999999999999999999999999999999999E+6144'
        string(decimal(dyn('1.50'))) => '1.50'
        decimal('1.50') == 1.5m => true
        """;

    private const string InvalidStrings = """
        Evaluate | decimal^('abc') | InvalidArgument | invalid argument to 'decimal': cannot parse abc as decimal
        Evaluate | decimal^('1e') | InvalidArgument | invalid argument to 'decimal': cannot parse 1e as decimal
        Evaluate | decimal^('e2') | InvalidArgument | invalid argument to 'decimal': cannot parse e2 as decimal
        Evaluate | decimal^('1.2.3') | InvalidArgument | invalid argument to 'decimal': cannot parse 1.2.3 as decimal
        Evaluate | decimal^('1_0') | InvalidArgument | invalid argument to 'decimal': cannot parse 1_0 as decimal
        Evaluate | decimal^('0x10') | InvalidArgument | invalid argument to 'decimal': cannot parse 0x10 as decimal
        Evaluate | decimal^('Infinity') | InvalidArgument | invalid argument to 'decimal': cannot parse Infinity as decimal
        Evaluate | decimal^('NaN') | InvalidArgument | invalid argument to 'decimal': cannot parse NaN as decimal
        Evaluate | decimal^('.') | InvalidArgument | invalid argument to 'decimal': cannot parse . as decimal
        Evaluate | decimal^('--1') | InvalidArgument | invalid argument to 'decimal': cannot parse --1 as decimal
        Evaluate | decimal^('1e99999999999x') | InvalidArgument | invalid argument to 'decimal': cannot parse 1e99999999999x as decimal
        Evaluate | decimal^('') | InvalidArgument | -
        Evaluate | decimal^(' 1') | InvalidArgument | -
        Evaluate | decimal^('1 ') | InvalidArgument | -
        """;

    // The exponent after the rounding is out of the range: ConversionRange, in a zero as well, with the string as written.
    private const string StringsOutOfTheRange = """
        Evaluate | decimal^('1e-6200') | ConversionRange | 1e-6200 is out of the range of decimal
        Evaluate | decimal^('1e-6177') | ConversionRange | 1e-6177 is out of the range of decimal
        Evaluate | decimal^('1e6112') | ConversionRange | 1e6112 is out of the range of decimal
        Evaluate | decimal^('0e6112') | ConversionRange | 0e6112 is out of the range of decimal
        Evaluate | decimal^('0e-6177') | ConversionRange | 0e-6177 is out of the range of decimal
        Evaluate | decimal^('1e99999999999') | ConversionRange | 1e99999999999 is out of the range of decimal
        Evaluate | decimal^('1e-99999999999') | ConversionRange | 1e-99999999999 is out of the range of decimal
        """;

    // decimal(decimal) is the identity: the exponent and the sign of a zero are those of the argument.
    private const string Identity = $$"""
        string(decimal(1.50m)) => '1.50'
        string(decimal(-0.0m)) => '-0.0'
        string(decimal(-0m)) => '-0'
        string(decimal(1e2m)) => '1E+2'
        string(decimal(0m)) => '0'
        string(decimal({{Max34}}e6111m)) => '9.999999999999999999999999999999999E+6144'
        string(decimal(dyn(1.50m))) => '1.50'
        decimal(1.5m) == 1.50m => true
        """;

    private const string NoOverload = """
        Evaluate | decimal^(dyn(true)) | EvalNoMatchingOverload | no such overload: decimal(bool)
        Check | decimal^(true) | NoMatchingOverload | found no matching overload for 'decimal' applied to '(bool)'
        Check | decimal^() | NoMatchingOverload | found no matching overload for 'decimal' applied to '()'
        Check | decimal^(1, 2) | NoMatchingOverload | found no matching overload for 'decimal' applied to '(int, int)'
        Check | decimal^('1', '2') | NoMatchingOverload | found no matching overload for 'decimal' applied to '(string, string)'
        Check | decimal^([1]) | NoMatchingOverload | found no matching overload for 'decimal' applied to '(list(int))'
        """;

    private const string ToInt = """
        int(1.9m) => 1
        int(-1.9m) => -1
        int(0.5m) => 0
        int(-0.5m) => 0
        int(100m) => 100
        int(1e2m) => 100
        int(-0m) => 0
        int(1e-6176m) => 0
        int(9223372036854775807m) => 9223372036854775807
        int(-9223372036854775808m) => -9223372036854775808
        int(9223372036854775807.9m) => 9223372036854775807
        int(-9223372036854775808.9m) => -9223372036854775808
        int(dyn(2.5m)) => 2
        """;

    private const string IntOutOfTheRange = $$"""
        Evaluate | int^(9223372036854775808m) | ConversionRange | 9223372036854775808 is out of the range of int
        Evaluate | int^(-9223372036854775809m) | ConversionRange | -9223372036854775809 is out of the range of int
        Evaluate | int^(9223372036854775808.5m) | ConversionRange | 9223372036854775808.5 is out of the range of int
        Evaluate | int^(1e19m) | ConversionRange | 1E+19 is out of the range of int
        Evaluate | int^(1e6111m) | ConversionRange | 1E+6111 is out of the range of int
        Evaluate | int^({{Max34}}m) | ConversionRange | {{Max34}} is out of the range of int
        """;

    // The sign is tested after the truncation: a negative value that truncates to zero is 0u, unlike uint(-0.5) of a double.
    private const string ToUint = """
        uint(1.9m) => 1u
        uint(0.5m) => 0u
        uint(-0.5m) => 0u
        uint(-0.99m) => 0u
        uint(-0.0m) => 0u
        uint(-0m) => 0u
        uint(100m) => 100u
        uint(18446744073709551615m) => 18446744073709551615u
        uint(18446744073709551615.9m) => 18446744073709551615u
        uint(dyn(2.5m)) => 2u
        """;

    private const string UintOutOfTheRange = """
        Evaluate | uint^(-1.5m) | ConversionRange | -1.5 is out of the range of uint
        Evaluate | uint^(-1m) | ConversionRange | -1 is out of the range of uint
        Evaluate | uint^(18446744073709551616m) | ConversionRange | 18446744073709551616 is out of the range of uint
        Evaluate | uint^(1e20m) | ConversionRange | 1E+20 is out of the range of uint
        """;

    private const string ToDouble = """
        double(1.5m) => 1.5
        double(-2.5m) => -2.5
        double(0.1m) => 0.1
        double(0.3m) == 0.3 => true
        double(100m) => 100.0
        double(1e2m) => 100.0
        double(1e308m) == 1e308 => true
        double(1.7976931348623157e308m) == 1.7976931348623157e308 => true
        double(9007199254740993m) == 9007199254740992.0 => true
        double(9007199254740995m) == 9007199254740996.0 => true
        double(1e-400m) == 0.0 => true
        1.0 / double(-0m) < 0.0 => true
        1.0 / double(0m) > 0.0 => true
        double(dyn(1.5m)) => 1.5
        """;

    private const string DoubleOutOfTheRange = """
        Evaluate | double^(1e309m) | ConversionRange | 1E+309 is out of the range of double
        Evaluate | double^(-1e309m) | ConversionRange | -1E+309 is out of the range of double
        Evaluate | double^(1e6111m) | ConversionRange | 1E+6111 is out of the range of double
        """;

    private const string ToStringRows = """
        string(1.5m) => '1.5'
        string(dyn(1.50m)) => '1.50'
        string(-0m) => '-0'
        string(decimal(1) / decimal(3)).size() => 36
        """;

    private readonly CelEnvironment environment = DecimalExample.Create();

    [Fact]
    public void DecimalOfAnIntAndOfAUint_IsExact() => Assert.Empty(ExtensionRows.CheckValues(environment, FromIntegers));

    [Fact]
    public void DecimalOfADouble_TakesTheDigitsOfStringOfTheDouble() => Assert.Empty(ExtensionRows.CheckValues(environment, FromDoubles));

    [Fact]
    [Trait("Requirement", "F-4")]
    public void DecimalOfANaNOrAnInfinity_IsConversionRange() => Assert.Empty(ExtensionRows.CheckErrors(environment, NotFinite));

    [Fact]
    [Trait("Requirement", "R-5")]
    public void DecimalOfAString_ReadsTheGrammar_AndRoundsMoreThan34Digits() => Assert.Empty(ExtensionRows.CheckValues(environment, FromStrings));

    [Fact]
    [Trait("Requirement", "F-4")]
    public void DecimalOfAStringOutsideTheGrammar_IsInvalidArgument() => Assert.Empty(ExtensionRows.CheckErrors(environment, InvalidStrings));

    [Fact]
    [Trait("Requirement", "F-4")]
    [Trait("Requirement", "R-5")]
    public void DecimalOfAStringWhoseExponentIsOutOfTheRange_IsConversionRange_AZeroToo() =>
        Assert.Empty(ExtensionRows.CheckErrors(environment, StringsOutOfTheRange));

    [Fact]
    public void DecimalOfADecimal_KeepsTheExponentAndTheSignOfTheZero() => Assert.Empty(ExtensionRows.CheckValues(environment, Identity));

    [Fact]
    [Trait("Requirement", "F-4")]
    [Trait("Requirement", "+R-6")]
    public void DecimalOfAValueNoOverloadTakes_IsNoMatchingOverload() => Assert.Empty(ExtensionRows.CheckErrors(environment, NoOverload));

    [Fact]
    public void IntOfADecimal_TruncatesTowardZero() => Assert.Empty(ExtensionRows.CheckValues(environment, ToInt));

    [Fact]
    [Trait("Requirement", "F-4")]
    public void IntOfADecimalOutOfTheRange_IsConversionRange() => Assert.Empty(ExtensionRows.CheckErrors(environment, IntOutOfTheRange));

    [Fact]
    public void UintOfADecimal_TruncatesFirst_AndChecksTheSignAfterwards() => Assert.Empty(ExtensionRows.CheckValues(environment, ToUint));

    [Fact]
    [Trait("Requirement", "F-4")]
    public void UintOfADecimalOutOfTheRange_IsConversionRange() => Assert.Empty(ExtensionRows.CheckErrors(environment, UintOutOfTheRange));

    [Fact]
    public void DoubleOfADecimal_IsTheNearestDouble() => Assert.Empty(ExtensionRows.CheckValues(environment, ToDouble));

    [Fact]
    [Trait("Requirement", "F-4")]
    public void DoubleOfADecimalBeyondTheRangeOfDouble_IsConversionRange() => Assert.Empty(ExtensionRows.CheckErrors(environment, DoubleOutOfTheRange));

    [Fact]
    public void StringOfADecimal_IsTheScientificText() => Assert.Empty(ExtensionRows.CheckValues(environment, ToStringRows));
}
#endif

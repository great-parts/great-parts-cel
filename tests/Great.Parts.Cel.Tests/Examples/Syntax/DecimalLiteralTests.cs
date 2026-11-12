#if NET11_0_OR_GREATER
using System;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Tests.Examples.Library;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Syntax;

// The literal of the type decimal and its option (the page of the extension set decimal, "The literal and the flag"; the page of the
// syntax, "Decimal literals"). The two forms are DIGIT* . DIGIT+ EXPONENT? and DIGIT+ EXPONENT?, each with the suffix m or M:
// 12.34m, .5m, 1e3m, 100m, 0m, 100M. The value is exact — the digits and the exponent are kept, so the cohort is kept — and a
// row that shows it is written as string(x): string(100m) is "100", string(1e2m) is "1E+2", string(0m) is "0", string(-0m) is
// "-0", string(-0.0m) is "-0.0". The sign is the unary minus, which the parser folds into the literal. A literal with more than
// 34 significant digits, whatever the form, or with an exponent out of the range of decimal128 (written exponent minus the digits
// of the fraction, from -6176 to 6111) is InvalidLiteral at the literal, and is not rounded; leading zeros are not significant.
// There is no hexadecimal form, no digit separator, and nothing may follow the suffix: 0x10m, 1_000m, 12.5mx are SyntaxError at
// the token that cannot go on, with the option and without it, and 1.m is the selection of a field m of the value 1, which type
// checking refuses. Without the option, in the environment of any set, both forms are SyntaxNotEnabled (1007) at the literal —
// at the minus when the literal has one — with the text that names the option, and the parser goes on, so every literal of a text
// is an error, in the order of the text; the function decimal('12.34') needs the set only. The values follow the page (there is no
// decimal in cel-go and no oracle for it); the rows that fix a place follow the places of the literals of int and double. Rows are
// those of ExtensionRows; the digit strings are written by constants, Max34 is 34 nines.
[Trait("Requirement", "F-5")]
[Trait("Requirement", "S-2")]
public sealed class DecimalLiteralTests
{
    private const string Max34 = DecimalExample.Max34;
    private const string Digits35 = "12345678901234567890123456789012345";
    private const string OneAnd39Zeros = "1000000000000000000000000000000000000000";
    private const string Zeros40 = "0000000000000000000000000000000000000000";
    private const string ZerosAndOne = "000000000000000000000000000000000000001";
    private const string TooMany = "invalid decimal literal: more than 34 significant digits";
    private const string OutOfRange = "invalid decimal literal: exponent out of the range of decimal128";
    private const string NotEnabled = "decimal literal is not enabled: enable decimal literals in the environment builder";

    // Both forms and the suffix in both cases, the plain and the scientific text of the value, and the cohort that is kept.
    private const string Forms = """
        string(100m) => '100'
        string(0m) => '0'
        string(100M) => '100'
        string(1e2m) => '1E+2'
        string(1E2M) => '1E+2'
        string(1e+2m) => '1E+2'
        string(1E-2m) => '0.01'
        string(-0m) => '-0'
        string(12.34m) => '12.34'
        string(.5m) => '0.5'
        string(1e3m) => '1E+3'
        string(1e-3m) => '0.001'
        string(2.5E+3M) => '2.5E+3'
        string(-0.0m) => '-0.0'
        string(1.50m) => '1.50'
        string(1.5m) => '1.5'
        string(100.0m) => '100.0'
        string(0.0m) => '0.0'
        string(0.000m) => '0.000'
        string(1000m) => '1000'
        string(007m) => '7'
        string(00012.50m) => '12.50'
        string(0.000005m) => '0.000005'
        string(0.0000005m) => '5E-7'
        string(5e-7m) => '5E-7'
        string(12345e-9m) => '0.000012345'
        string(0e3m) => '0E+3'
        string(0e-8m) => '0E-8'
        [1.5m, 2m].size() => 2
        """;

    // The value is the number, and the cohorts of one number are equal and differ in their text.
    private const string Equality = """
        100m == 1e2m => true
        100m == 100.0m => true
        1.50m == 1.5m => true
        1e3m == 1000m => true
        -0m == 0m => true
        -0.0m == 0m => true
        100m != 1e2m => false
        100m == 101m => false
        string(100m) == string(1e2m) => false
        string(1.50m) == string(1.5m) => false
        string(-0m) == string(0m) => false
        """;

    // The sign is the unary minus: a single minus before the token is part of the literal, and the call of the operator on a
    // parenthesized literal gives the same value.
    private const string Signs = """
        string(-12.34m) => '-12.34'
        string(--5m) => '5'
        string(-(0m)) => '-0'
        string(-(-0m)) => '0'
        string(-(0.0m)) => '-0.0'
        string(-1e2m) => '-1E+2'
        -(1.5m) == -1.5m => true
        """;

    // The edges: 34 significant digits and the whole range of the exponent. The exponent is the written one minus the digits of the
    // fraction, so 1.5e6112m has the exponent 6111 and 0.5e-6175m the exponent -6176, which are the last that decimal128 has.
    private const string Edges = $$"""
        string({{Max34}}m) => '{{Max34}}'
        string(1234567890123456789012345678901230m) => '1234567890123456789012345678901230'
        string(1e39m) => '1E+39'
        string(1e-6176m) => '1E-6176'
        string(1e6111m) => '1E+6111'
        string({{Max34}}e6111m) => '9.999999999999999999999999999999999E+6144'
        string(1.5e6112m) => '1.5E+6112'
        string(0.5e-6175m) => '5E-6176'
        string({{Zeros40}}12m) => '12'
        string({{Zeros40}}.5m) => '0.5'
        string(0.{{Zeros40}}1m) => '1E-41'
        string({{ZerosAndOne}}m) => '1'
        """;

    // More than 34 significant digits, in either form and with an exponent, and an exponent out of the range, at the literal; the minus
    // belongs to the literal, so the place of a signed one is the minus (as for int). Nothing is rounded.
    private const string InvalidLiterals = $$"""
        Parse | ^{{Digits35}}m | InvalidLiteral | {{TooMany}}
        Parse | ^{{OneAnd39Zeros}}m | InvalidLiteral | {{TooMany}}
        Parse | ^1.2345678901234567890123456789012345m | InvalidLiteral | {{TooMany}}
        Parse | ^{{Digits35}}e-5m | InvalidLiteral | {{TooMany}}
        Parse | ^{{Max34}}1m | InvalidLiteral | {{TooMany}}
        Parse | ^-{{Digits35}}m | InvalidLiteral | {{TooMany}}
        Parse | 1 + ^{{Digits35}}m | InvalidLiteral | {{TooMany}}
        Parse | ^1e6112m | InvalidLiteral | {{OutOfRange}}
        Parse | ^1e-6177m | InvalidLiteral | {{OutOfRange}}
        Parse | ^0e6112m | InvalidLiteral | {{OutOfRange}}
        Parse | ^0e-6177m | InvalidLiteral | {{OutOfRange}}
        Parse | ^{{Max34}}e6112m | InvalidLiteral | {{OutOfRange}}
        Parse | ^0.5e-6176m | InvalidLiteral | {{OutOfRange}}
        Parse | ^1e99999999999m | InvalidLiteral | {{OutOfRange}}
        Parse | ^1e-99999999999m | InvalidLiteral | {{OutOfRange}}
        Parse | ^-1e6112m | InvalidLiteral | {{OutOfRange}}
        Parse | ^1e6112m + ^{{Digits35}}m | InvalidLiteral InvalidLiteral | {{OutOfRange}} ;; {{TooMany}}
        """;

    // Without the option, in the environment of the set: the literal is recognized and refused, at its first character.
    private const string WithoutTheOption = $$"""
        Parse | ^12.34m | SyntaxNotEnabled | {{NotEnabled}}
        Parse | ^100m | SyntaxNotEnabled | {{NotEnabled}}
        Parse | ^0m | SyntaxNotEnabled | {{NotEnabled}}
        Parse | ^100M | SyntaxNotEnabled | {{NotEnabled}}
        Parse | ^1e2m | SyntaxNotEnabled | {{NotEnabled}}
        Parse | ^.5m | SyntaxNotEnabled | {{NotEnabled}}
        Parse | ^2.5E+3M | SyntaxNotEnabled | {{NotEnabled}}
        Parse | ^-100m | SyntaxNotEnabled | {{NotEnabled}}
        Parse | 1 + ^100m + ^12.34m | SyntaxNotEnabled SyntaxNotEnabled | {{NotEnabled}} ;; {{NotEnabled}}
        Parse | [^1.5m, 2] | SyntaxNotEnabled | {{NotEnabled}}
        Parse | decimal('1') + ^1m | SyntaxNotEnabled | {{NotEnabled}}
        """;

    // 1.m is the selection of the field m of the value 1: the literal needs a digit after the point.
    private const string Selection = """
        Check | 1^.m | NoSuchField | undefined field 'm' in type 'int'
        Check | 1^.mm | NoSuchField | undefined field 'mm' in type 'int'
        """;

    private readonly CelEnvironment enabled = DecimalExample.Create();

    private readonly CelEnvironment disabled = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Decimal(1))
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public void BothFormsAndBothSuffixes_KeepTheDigitsAndTheExponent() => Assert.Empty(ExtensionRows.CheckValues(enabled, Forms));

    [Fact]
    public void CohortsOfOneNumber_AreEqual() => Assert.Empty(ExtensionRows.CheckValues(enabled, Equality));

    [Fact]
    public void Minus_IsTheUnaryMinus() => Assert.Empty(ExtensionRows.CheckValues(enabled, Signs));

    [Fact]
    public void EdgesOf34DigitsAndOfTheExponent_AreLiterals() => Assert.Empty(ExtensionRows.CheckValues(enabled, Edges));

    [Fact]
    [Trait("Requirement", "F-4")]
    [Trait("Requirement", "F-8")]
    public void MoreThan34DigitsOrAnExponentOutOfRange_IsInvalidLiteralAtTheLiteral() =>
        Assert.Empty(ExtensionRows.CheckErrors(enabled, InvalidLiterals));

    // The forms that are no literal of decimal: the number and the letters after it are two tokens, and the first error is at the
    // second token. The same with the option and without it; the text of the error is that of any syntax error. Only the first
    // error is asserted: the parser may report more after it.
    [Theory]
    [Trait("Requirement", "F-4")]
    [InlineData("0x10m", 4)]
    [InlineData("0x10M", 4)]
    [InlineData("12.5mx", 4)]
    [InlineData("100mx", 3)]
    [InlineData("12.34mm", 5)]
    [InlineData("1_000m", 1)]
    public void HexadecimalFormAndTextAfterTheSuffix_AreSyntaxErrors_WithTheOptionAndWithoutIt(string expression, int offset)
    {
        foreach (CelEnvironment environment in new[] { enabled, disabled })
        {
            ParseResult result = environment.Parse(expression);

            Assert.False(result.IsSuccess);
            CelError error = result.Errors[0];
            Assert.Equal((CelErrorCode.SyntaxError, CelErrorStage.Parse, offset), (error.Code, error.Stage, error.Location.Offset));
            Assert.StartsWith("syntax error: ", error.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Requirement", "F-4")]
    [Trait("Requirement", "R-5")]
    public void EveryLiteralWithoutTheOption_IsSyntaxNotEnabledAtTheLiteral() =>
        Assert.Empty(ExtensionRows.CheckErrors(disabled, WithoutTheOption));

    [Fact]
    public void FunctionDecimalOfAString_NeedsTheSetAndNotTheOption() =>
        Assert.Empty(ExtensionRows.CheckValues(disabled, "string(decimal('12.34')) => '12.34'"));

    [Fact]
    [Trait("Requirement", "F-4")]
    public void OneDotLetter_IsTheSelectionOfAField_AndTypeCheckingRefusesIt() => Assert.Empty(ExtensionRows.CheckErrors(enabled, Selection));
}
#endif

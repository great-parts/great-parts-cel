using Great.Parts.Cel.Tests.Examples.Library;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Checking;

// The check of a call of format of the strings set at compilation, InvalidFormatString (2010). Only a call
// whose format string is a string constant and whose argument is a list literal is checked, after the types are
// checked and the overload string_format is chosen: a variable in either place leaves the call unchecked, and an
// error of the format is then InvalidArgument at evaluation, with the same detail. The checked call is an error
// when the format string is wrong (an unknown letter, a precision over 100, no element of the list for a
// specifier), when the type of an element does not fit its specifier (the type dyn fits every one) and when
// there are more elements than specifiers; the error is at the call, the "(" after the name, except the error
// of the type of an element, which is at that element of the list literal, and every checked call of the text
// is reported, in the order of the text. At evaluation the elements past the specifiers
// are not an error. The texts are those of cel-go v0.32.0 behind "invalid format string: ", which are the
// details of the page of the error codes; each case is an error of cel-go at the same place (a probe of
// the oracle with the flags of the conformance runner, 2026-11-09).
[Trait("Requirement", "F-1")]
[Trait("Requirement", "F-4")]
[Trait("Requirement", "F-8")]
public sealed class FormatCheckTests
{
    private const string Clause = "invalid format string: could not parse formatting clause: ";

    private const string Precision = Clause + "error while parsing precision: precision 101 exceeds maximum allowed precision 100";

    private const string Decimal = "invalid format string: error during formatting: decimal clause can only be used on ints, uints, and doubles, was given ";

    private const string Checked = $$"""
        Check | '%z'.format^([1]) | InvalidFormatString | {{Clause}}unrecognized formatting clause "z"
        Check | 'a%q'.format^([1]) | InvalidFormatString | {{Clause}}unrecognized formatting clause "q"
        Check | '%5d'.format^([1]) | InvalidFormatString | {{Clause}}unrecognized formatting clause "5"
        Check | '%.101f'.format^([1.5]) | InvalidFormatString | {{Precision}}
        Check | '%.101s'.format^(['a']) | InvalidFormatString | {{Precision}}
        Check | '%.101e'.format^([1.5]) | InvalidFormatString | {{Precision}}
        Check | '%.101d'.format^([1]) | InvalidFormatString | {{Precision}}
        Check | '%d %d'.format^([1]) | InvalidFormatString | invalid format string: index 1 out of range
        Check | '%s %s'.format^(['a']) | InvalidFormatString | invalid format string: index 1 out of range
        Check | '%s'.format^([]) | InvalidFormatString | invalid format string: index 0 out of range
        Check | '%'.format^([1]) | InvalidFormatString | invalid format string: unexpected end of string
        Check | 'abc%'.format^([]) | InvalidFormatString | invalid format string: unexpected end of string
        Check | '%.f'.format^([1]) | InvalidFormatString | {{Clause}}error while parsing precision: error while converting precision to integer: invalid syntax
        Check | '%.5'.format^([1]) | InvalidFormatString | {{Clause}}error while parsing precision: could not find end of precision specifier
        Check | '%d'.format([^'a']) | InvalidFormatString | {{Decimal}}string
        Check | '%d'.format([^true]) | InvalidFormatString | {{Decimal}}bool
        Check | '%f'.format([^'a']) | InvalidFormatString | invalid format string: error during formatting: fixed-point clause can only be used on ints, uints, and doubles, was given string
        Check | '%e'.format([^[1]]) | InvalidFormatString | invalid format string: error during formatting: scientific clause can only be used on ints, uints, and doubles, was given list
        Check | '%b'.format([^1.5]) | InvalidFormatString | invalid format string: error during formatting: only ints, uints, and bools can be formatted as binary, was given double
        Check | '%o'.format([^'a']) | InvalidFormatString | invalid format string: error during formatting: octal clause can only be used on ints and uints, was given string
        Check | '%x'.format([^1.5]) | InvalidFormatString | invalid format string: error during formatting: only ints, uints, bytes, and strings can be formatted as hex, was given double
        Check | '%d'.format^([1, 2]) | InvalidFormatString | invalid format string: too many arguments supplied to string.format (expected 1, got 2)
        Check | 'abc'.format^([1]) | InvalidFormatString | invalid format string: too many arguments supplied to string.format (expected 0, got 1)
        Check | '%X'.format([^true]) | InvalidFormatString | invalid format string: error during formatting: only ints, uints, bytes, and strings can be formatted as hex, was given bool
        Check | '%%'.format^(['a']) | InvalidFormatString | invalid format string: too many arguments supplied to string.format (expected 0, got 1)
        Check | '%d %d'.format([1, ^'a']) | InvalidFormatString | {{Decimal}}string
        Check | '%d %s'.format^([1, 'a', 2]) | InvalidFormatString | invalid format string: too many arguments supplied to string.format (expected 2, got 3)
        Check | ['a'].map(x, '%d'.format([^x])) | InvalidFormatString | {{Decimal}}string
        Check | '%z'.format^([1]) + '%d'.format([^'a']) | InvalidFormatString InvalidFormatString | {{Clause}}unrecognized formatting clause "z" ;; {{Decimal}}string
        Check | size('%z'.format^([1])) > 0 | InvalidFormatString | {{Clause}}unrecognized formatting clause "z"
        """;

    // The same errors at evaluation when the format string or the list is not a constant or a literal: InvalidArgument at
    // the call with the details of the compilation behind "invalid argument to 'format': ". An element of the
    // type dyn passes the compilation, and its value is looked at when the call is evaluated.
    private const string Unchecked = """
        Evaluate | fz.format^([1]) | InvalidArgument | invalid argument to 'format': could not parse formatting clause: unrecognized formatting clause "z"
        Evaluate | fn.format^([1]) | InvalidArgument | invalid argument to 'format': index 1 out of range
        Evaluate | fd.format^(['a']) | InvalidArgument | invalid argument to 'format': error during formatting: decimal clause can only be used on ints, uints, and doubles, was given string
        Evaluate | fd.format^(texts) | InvalidArgument | invalid argument to 'format': error during formatting: decimal clause can only be used on ints, uints, and doubles, was given string
        Evaluate | '%d'.format^(texts) | InvalidArgument | invalid argument to 'format': error during formatting: decimal clause can only be used on ints, uints, and doubles, was given string
        Evaluate | '%d'.format^([dyn('a')]) | InvalidArgument | invalid argument to 'format': error during formatting: decimal clause can only be used on ints, uints, and doubles, was given string
        Evaluate | '%z'.format^(texts) | InvalidArgument | invalid argument to 'format': could not parse formatting clause: unrecognized formatting clause "z"
        """;

    // A call that is checked and right, or not checked and right, is a value; the elements past the specifiers are
    // not an error at evaluation. A precision is read for every specifier but acts for %f and %e only.
    private const string Values = """
        'abc'.format([]) => 'abc'
        '%%'.format([]) => '%'
        '%s-%d'.format(['a', 1]) => 'a-1'
        '%s'.format([1]) => '1'
        '%s'.format([null]) => 'null'
        '%s'.format([[1, 'a']]) => '[1, a]'
        '%d'.format([dyn(1)]) => '1'
        '%b'.format([5]) => '101'
        '%o %x %X'.format([8, 255, 255]) => '10 ff FF'
        '%e'.format([1234.5678]) => '1.234568e+03'
        '%.2f'.format([3.14159]) => '3.14'
        '%.3f %.1e'.format([1.5, 1234.5]) => '1.500 1.2e+03'
        '%.3s'.format(['abcdef']) => 'abcdef'
        '%.100s'.format(['a']) => 'a'
        fd.format([1, 2]) => '1'
        fd.format(numbers) => '1'
        fs.format(['a']) => 'a'
        ['a', 'b'].map(x, '%s!'.format([x])) => ['a!', 'b!']
        [1, 2].map(x, '%d'.format([x])) == ['1', '2'] => true
        """;

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddExtension(CelExtensions.Strings(5))
        .AddVariable<string>("fz")
        .AddVariable<string>("fn")
        .AddVariable<string>("fd")
        .AddVariable<string>("fs")
        .AddVariable("texts", CelType.ListOf(CelType.Dyn))
        .AddVariable("numbers", CelType.ListOf(CelType.Dyn))
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelBindings bindings = new CelBindings()
        .Add("fz", "%z")
        .Add("fn", "%d %d")
        .Add("fd", "%d")
        .Add("fs", "%s")
        .Add("texts", CelList.Create(CelValue.FromString("a")))
        .Add("numbers", CelList.Create(CelValue.FromInt64(1), CelValue.FromInt64(2)));

    [Fact]
    public void CheckedCall_WithAnInvalidFormat_IsInvalidFormatStringAtTheCall()
    {
        Assert.Empty(ExtensionRows.CheckErrors(environment, Checked, bindings));
    }

    [Fact]
    public void UncheckedCall_WithAnInvalidFormat_IsInvalidArgumentAtEvaluation()
    {
        Assert.Empty(ExtensionRows.CheckErrors(environment, Unchecked, bindings));
    }

    // Each of these compiles: the format string or the list is not a constant or a literal, or the type of the element
    // is dyn, so the compiler does not read the format.
    [Theory]
    [InlineData("fz.format([1])")]
    [InlineData("fn.format([1])")]
    [InlineData("fd.format(['a'])")]
    [InlineData("fd.format(texts)")]
    [InlineData("'%d'.format(texts)")]
    [InlineData("'%z'.format(texts)")]
    [InlineData("'%d'.format([dyn('a')])")]
    [InlineData("fd.format([1, 2])")]
    [InlineData("'%d %d'.format(texts)")]
    [InlineData("'%.101f'.format(texts)")]
    public void CallThatIsNotChecked_Compiles(string expression)
    {
        Assert.True(environment.Compile<CelValue>(expression).IsSuccess, expression);
    }

    [Fact]
    public void CallThatIsRight_GivesItsValue()
    {
        Assert.Empty(ExtensionRows.CheckValues(environment, Values, bindings));
    }
}

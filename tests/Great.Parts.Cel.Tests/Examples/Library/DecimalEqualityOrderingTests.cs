#if NET11_0_OR_GREATER
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Library;

// Equality and order of the type decimal (the page of the extension set decimal, "The 18 overloads"; the page of the values, "decimal"):
// two decimals are equal when they are the same number — the cohort and the sign of a zero do not matter, so 1.50 == 1.5 and
// -0 == 0 — and are ordered by the number: the four orderings _<_, _<=_, _>_ and _>=_ of the set (less_decimal, less_equals_decimal,
// greater_decimal, greater_equals_decimal). A decimal equals and orders only with a decimal. Under dyn a decimal is not equal to an
// int, a uint, a double or any value of another type, without an error: dyn(1.0m) == 1 is false and dyn(1.0m) != 1 is true; the
// same rule gives the in operator and the equality of lists and maps that hold decimals. The order with another type under dyn is
// the error EvalNoMatchingOverload (3000), "no such overload: _<_(decimal, int)", at the operator, whichever side the decimal is
// on; so is every other function of the library that has no overload for a decimal — a sort of a list of decimals (lists.sort), the
// operators _+_ and _*_ with an int — with the set and without it. A static null is assignable to decimal, as it is to every abstract
// type, so p == null compiles and is false. A key of a map literal that is a decimal is UnsupportedMapKeyType (3027), "unsupported
// map key type 'decimal'", at the key; the check does not refuse it. The type of a decimal is decimal: type(1.5m) == decimal. Rows
// are those of ExtensionRows.
[Trait("Requirement", "F-5")]
[Trait("Requirement", "S-2")]
[Trait("Requirement", "R-5")]
public sealed class DecimalEqualityOrderingTests
{
    private const string Equality = """
        1.50m == 1.5m => true
        1.5m == 1.50m => true
        -0m == 0m => true
        -0.0m == 0m => true
        0.0m == -0.00m => true
        100m == 1e2m => true
        1.5m != 1.50m => false
        1.5m == 2.5m => false
        1.5m != 2.5m => true
        0.1m + 0.2m == 0.3m => true
        decimal(1) == decimal(1u) => true
        decimal(1) == decimal('1.0') => true
        string(1.50m) == string(1.5m) => false
        [1.5m, 2.5m] == [1.50m, 2.50m] => true
        [1.5m, 2.5m] == [2.5m, 1.5m] => false
        {'a': 1.5m} == {'a': 1.50m} => true
        {'a': 1.5m}['a'] == 1.50m => true
        [1.5m, 2.5m][1] == 2.50m => true
        1.5m in [1.50m, 2.5m] => true
        """;

    private const string Order = $$"""
        1.5m < 2.5m => true
        1.5m < 1.50m => false
        1.5m <= 1.50m => true
        1.5m > 1.50m => false
        1.5m >= 1.50m => true
        2.5m > 1.5m => true
        -1.5m < 1.5m => true
        -1.5m < -1.4m => true
        -0m < 0m => false
        -0m <= 0m => true
        -0m > 0m => false
        0m >= -0m => true
        1e2m > 99m => true
        1e2m > 100m => false
        1e-6176m > 0m => true
        {{DecimalExample.Max34}}e6111m > 1e6111m => true
        -{{DecimalExample.Max34}}e6111m < -1e6111m => true
        0.1m + 0.2m > 0.3m => false
        """;

    // A decimal is not a number of the other kinds under dyn: not equal, without an error; not ordered, with the error.
    private const string DynEquality = """
        dyn(1.0m) == 1 => false
        dyn(1.0m) == 1u => false
        dyn(1.0m) == 1.0 => false
        dyn(1.0m) == '1.0' => false
        dyn(1.0m) == true => false
        dyn(1.0m) != 1 => true
        dyn(1.0m) != 1.0 => true
        1 == dyn(1.0m) => false
        1.0 == dyn(1.0m) => false
        1u != dyn(1.0m) => true
        dyn(1.0m) == dyn(1.0m) => true
        dyn(1.5m) == dyn(1.50m) => true
        dyn(1.5m) == 1.50m => true
        1.5m == dyn(1.50m) => true
        dyn(-0m) == dyn(0m) => true
        dyn([1.5m]) == dyn([1.50m]) => true
        dyn([1.5m]) == [1] => false
        dyn({'a': 1.5m}) == {'a': 1} => false
        dyn(1.0m) in [1, 2] => false
        dyn(1.0m) in [1.0m, 2] => true
        1.5m in [1, 1.50m] => true
        1.5m in [1, 2.5m] => false
        dyn(1.5m) in {1: 2} => false
        dyn(1.5m) in {'a': 2} => false
        """;

    private const string Nulls = """
        1.5m == null => false
        null == 1.5m => false
        1.5m != null => true
        dyn(1.0m) == null => false
        null != dyn(1.0m) => true
        """;

    private const string Types = """
        type(1.5m) == decimal => true
        type(1.5m) == type(2m) => true
        type(decimal(1)) == decimal => true
        type(dyn(1.5m)) == decimal => true
        type(1.5m) == int => false
        type(1) == decimal => false
        type(1.5m) != double => true
        """;

    private const string OrderErrors = """
        Evaluate | dyn(1.5m) ^< 1 | EvalNoMatchingOverload | no such overload: _<_(decimal, int)
        Evaluate | dyn(1.5m) ^<= 1u | EvalNoMatchingOverload | no such overload: _<=_(decimal, uint)
        Evaluate | dyn(1.5m) ^> 1.5 | EvalNoMatchingOverload | no such overload: _>_(decimal, double)
        Evaluate | dyn(1.5m) ^>= 'a' | EvalNoMatchingOverload | no such overload: _>=_(decimal, string)
        Evaluate | 1 ^< dyn(1.5m) | EvalNoMatchingOverload | no such overload: _<_(int, decimal)
        Evaluate | 1.5 ^> dyn(1.5m) | EvalNoMatchingOverload | no such overload: _>_(double, decimal)
        Evaluate | dyn(1.5m) ^< dyn(1) | EvalNoMatchingOverload | no such overload: _<_(decimal, int)
        Evaluate | dyn(1.5m) ^+ 1 | EvalNoMatchingOverload | no such overload: _+_(decimal, int)
        Evaluate | dyn(1) ^+ 1.5m | EvalNoMatchingOverload | no such overload: _+_(int, decimal)
        Evaluate | dyn(1.5m) ^* 2.0 | EvalNoMatchingOverload | no such overload: _*_(decimal, double)
        """;

    private const string MapKeys = """
        Evaluate | {^1.5m: 1} | UnsupportedMapKeyType | unsupported map key type 'decimal'
        Evaluate | {1: 1, ^1.5m: 2} | UnsupportedMapKeyType | unsupported map key type 'decimal'
        Evaluate | {'a': 1, ^2.5m: 2} | UnsupportedMapKeyType | unsupported map key type 'decimal'
        Evaluate | {dyn^(1.5m): 1} | UnsupportedMapKeyType | unsupported map key type 'decimal'
        """;

    // The sort of the set lists has no overload for a list of decimals: with dyn the call is found at run time and refused, at the "(".
    private const string SortUnderDyn = """
        Evaluate | dyn([2.5m, 1.5m]).sort^() | EvalNoMatchingOverload | -
        """;

    // math.greatest, math.least and sortBy are macros: they are expanded before the check, and the call they make has the "(" of the
    // macro call for its place. Under dyn the call finds no overload for decimal at run time. The text is fixed for one and two
    // arguments; for sortBy only the code and the place are.
    private const string MacrosUnderDyn = """
        Evaluate | math.greatest^(dyn(1.5m), dyn(2.5m)) | EvalNoMatchingOverload | no such overload: math.@max(decimal, decimal)
        Evaluate | math.least^(dyn(1.5m), dyn(2.5m)) | EvalNoMatchingOverload | no such overload: math.@min(decimal, decimal)
        Evaluate | math.greatest^(dyn(1.5m)) | EvalNoMatchingOverload | no such overload: math.@max(decimal)
        Evaluate | dyn([2.5m, 1.5m]).sortBy^(x, x) | EvalNoMatchingOverload | -
        """;

    private readonly CelEnvironment environment = DecimalExample.Create();

    private readonly CelEnvironment withLists = DecimalExample.Builder()
        .AddExtension(CelExtensions.Lists(4))
        .Build()
        .GetEnvironmentOrThrow();

    private readonly CelEnvironment withMath = DecimalExample.Builder()
        .AddExtension(CelExtensions.Math(3))
        .AddExtension(CelExtensions.Lists(4))
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public void TwoDecimals_AreEqualWhenTheyAreTheSameNumber() => Assert.Empty(ExtensionRows.CheckValues(environment, Equality));

    [Fact]
    public void TwoDecimals_AreOrderedByTheNumber() => Assert.Empty(ExtensionRows.CheckValues(environment, Order));

    [Fact]
    [Trait("Requirement", "F-8")]
    public void DecimalUnderDyn_IsNotEqualToAValueOfAnotherType_AndNeverAnError() => Assert.Empty(ExtensionRows.CheckValues(environment, DynEquality));

    [Fact]
    public void NullIsAssignableToDecimal_AndNeverEqualToIt() => Assert.Empty(ExtensionRows.CheckValues(environment, Nulls));

    [Fact]
    public void TypeOfADecimal_IsDecimal() => Assert.Empty(ExtensionRows.CheckValues(environment, Types));

    [Fact]
    [Trait("Requirement", "F-4")]
    [Trait("Requirement", "F-8")]
    public void OrderAndArithmeticWithAnotherTypeUnderDyn_IsEvalNoMatchingOverloadAtTheOperator() =>
        Assert.Empty(ExtensionRows.CheckErrors(environment, OrderErrors));

    [Fact]
    [Trait("Requirement", "F-4")]
    public void DecimalKeyOfAMapLiteral_IsUnsupportedMapKeyTypeAtTheKey() => Assert.Empty(ExtensionRows.CheckErrors(environment, MapKeys));

    [Fact]
    [Trait("Requirement", "F-4")]
    public void SortOfAListOfDecimalsUnderDyn_IsEvalNoMatchingOverload() => Assert.Empty(ExtensionRows.CheckErrors(withLists, SortUnderDyn));

    [Fact]
    [Trait("Requirement", "F-4")]
    public void MacrosOverDecimalsUnderDyn_AreEvalNoMatchingOverloadAtTheParenOfTheMacroCall() =>
        Assert.Empty(ExtensionRows.CheckErrors(withMath, MacrosUnderDyn));
}
#endif

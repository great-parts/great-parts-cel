#if NET11_0_OR_GREATER
using Great.Parts.Cel.Tests.Examples.Library;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Checking;

// The static type of the expressions of the set decimal and the errors of its overloads at the check (the page of the extension set
// decimal, "The 18 overloads", "Equality and order"; the page of type checking, "decimal without the set"): the literals, the
// operators, the orderings and equality, decimal() and the conversions give decimal, bool, int, uint, double and string as the
// signatures of the table say; the type of the type value is type(decimal), and the identifier decimal is that type value. A
// decimal is assignable to dyn, to a type parameter and to decimal, and to nothing else; null is assignable to it, as to every abstract
// type, so p == null checks. A decimal and another type meet in no overload: 1.5m + 1, price > 0 and price == 0 for a price of the
// type decimal are NoMatchingOverload (2001) at the operator, "found no matching overload for '<function>' applied to '(<types>)'",
// and so is the remainder %, which the set does not have, and decimal() of a type that it does not take, at the "(". The correct
// forms are price > 0m (with the option) and price > decimal(0). A list of a decimal and an int is list(dyn). Rows are those of
// ExtensionRows; p is a variable of the type decimal, d of dyn, l of list(decimal).
[Trait("Requirement", "F-5")]
[Trait("Requirement", "F-1")]
public sealed class DecimalCheckTests
{
    private const string Cases = """
        1.5m => decimal
        100m => decimal
        -0m => decimal
        decimal(1) => decimal
        decimal(1u) => decimal
        decimal(1.5) => decimal
        decimal('1.5') => decimal
        decimal(1.5m) => decimal
        decimal(d) => decimal
        1.5m + 2.5m => decimal
        1.5m - 2.5m => decimal
        1.5m * 2.5m => decimal
        1.5m / 2.5m => decimal
        -(1.5m) => decimal
        -p => decimal
        p + decimal(1) => decimal
        1.5m < 2.5m => bool
        1.5m <= 2.5m => bool
        1.5m > 2.5m => bool
        1.5m >= 2.5m => bool
        1.5m == 2.5m => bool
        1.5m != 2.5m => bool
        p > 0m => bool
        p > decimal(0) => bool
        p == null => bool
        int(1.5m) => int
        uint(1.5m) => uint
        double(1.5m) => double
        string(1.5m) => string
        type(1.5m) => type(decimal)
        decimal => type(decimal)
        type(p) == decimal => bool
        [1.5m, 2.5m] => list(decimal)
        [1.5m, 1] => list(dyn)
        {'a': 1.5m} => map(string, decimal)
        true ? 1.5m : 2.5m => decimal
        dyn(1.5m) => dyn
        l => list(decimal)
        l[0] => decimal
        [1.5m, 2.5m].map(x, x * 2m) => list(decimal)
        [1.5m, 2.5m].all(x, x > 1m) => bool
        [1.5m, 2.5m].filter(x, x > 2m) => list(decimal)
        """;

    private const string Mistakes = """
        Check | 1.5m ^+ 1 | NoMatchingOverload | found no matching overload for '_+_' applied to '(decimal, int)'
        Check | 1 ^+ 1.5m | NoMatchingOverload | found no matching overload for '_+_' applied to '(int, decimal)'
        Check | 1.5m ^+ 1.5 | NoMatchingOverload | found no matching overload for '_+_' applied to '(decimal, double)'
        Check | 1.5m ^+ 1u | NoMatchingOverload | found no matching overload for '_+_' applied to '(decimal, uint)'
        Check | 1.5m ^+ 'a' | NoMatchingOverload | found no matching overload for '_+_' applied to '(decimal, string)'
        Check | 1.5m ^- 1 | NoMatchingOverload | found no matching overload for '_-_' applied to '(decimal, int)'
        Check | 1.5m ^* 2 | NoMatchingOverload | found no matching overload for '_*_' applied to '(decimal, int)'
        Check | 1.5m ^/ 2 | NoMatchingOverload | found no matching overload for '_/_' applied to '(decimal, int)'
        Check | 1.5m ^< 1 | NoMatchingOverload | found no matching overload for '_<_' applied to '(decimal, int)'
        Check | 1.5m ^<= 1.5 | NoMatchingOverload | found no matching overload for '_<=_' applied to '(decimal, double)'
        Check | 1.5m ^> 1u | NoMatchingOverload | found no matching overload for '_>_' applied to '(decimal, uint)'
        Check | 1.5m ^>= 1 | NoMatchingOverload | found no matching overload for '_>=_' applied to '(decimal, int)'
        Check | p ^> 0 | NoMatchingOverload | found no matching overload for '_>_' applied to '(decimal, int)'
        Check | p ^== 0 | NoMatchingOverload | found no matching overload for '_==_' applied to '(decimal, int)'
        Check | p ^!= 0.0 | NoMatchingOverload | found no matching overload for '_!=_' applied to '(decimal, double)'
        Check | 1.5m ^% 1.5m | NoMatchingOverload | found no matching overload for '_%_' applied to '(decimal, decimal)'
        Check | decimal^(true) | NoMatchingOverload | found no matching overload for 'decimal' applied to '(bool)'
        Check | ^-'a' | NoMatchingOverload | found no matching overload for '-_' applied to '(string)'
        Check | p ^+ 1 + 1.5m | NoMatchingOverload | found no matching overload for '_+_' applied to '(decimal, int)'
        """;

    private readonly CelEnvironment environment = DecimalExample.Builder()
        .AddVariable("p", CelType.Decimal)
        .AddVariable("d", CelType.Dyn)
        .AddVariable("l", CelType.ListOf(CelType.Decimal))
        .Build()
        .GetEnvironmentOrThrow();

    public static TheoryData<string, string> Types
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (string line in ExtensionRows.Lines(Cases))
            {
                string[] parts = line.Split(" => ");
                data.Add(parts[0], parts[1]);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Types))]
    public void Expression_HasItsStaticType(string expression, string type)
    {
        CelProgram<CelValue> program = environment.Compile<CelValue>(expression).GetProgramOrThrow();

        Assert.Equal(type, program.ResultType.ToString());
    }

    // The type of the result is a value of CelType too: decimal is the abstract type with no parameters, and the type of its type value
    // is type(decimal).
    [Fact]
    public void ResultType_IsTheDecimalTypeOfCelType()
    {
        Assert.Equal(CelType.Decimal, environment.Compile<CelValue>("1.5m + 2.5m").GetProgramOrThrow().ResultType);
        Assert.Equal(CelType.Decimal, environment.Compile<CelValue>("decimal('1')").GetProgramOrThrow().ResultType);
        Assert.Equal(CelType.TypeOf(CelType.Decimal), environment.Compile<CelValue>("type(p)").GetProgramOrThrow().ResultType);
        Assert.Equal(CelType.ListOf(CelType.Decimal), environment.Compile<CelValue>("[p]").GetProgramOrThrow().ResultType);
        Assert.NotEqual(CelType.Double, CelType.Decimal);
        Assert.Equal("decimal", CelType.Decimal.ToString());
    }

    [Fact]
    [Trait("Requirement", "F-4")]
    [Trait("Requirement", "+R-6")]
    public void DecimalWithAnotherType_IsNoMatchingOverloadAtTheOperator() => Assert.Empty(ExtensionRows.CheckErrors(environment, Mistakes));
}
#endif

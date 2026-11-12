using System.Collections.Generic;
using Great.Parts.Cel.Tests.Examples.Library;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;
#if NET11_0_OR_GREATER
using System.Numerics;
using Great.Parts.Cel.Evaluation;
using Great.Parts.Cel.Syntax;
#endif

namespace Great.Parts.Cel.Tests.Evaluation;

// The places where a decimal that the environment cannot have is refused, besides the first read of a variable and the index of a list,
// which the examples of the environment without the set have: the value of a field of an object, the result of a function of the
// application, and the variable of a macro over such values. The value is a System.Decimal in both targets (a Decimal128 and the
// kind Decimal exist on .NET 11 alone); in .NET 10 it is an object that no type source describes, in .NET 11 a decimal that the
// environment has no set for, and the error is the same TypeNotAccessible at the node that read the value. With the set the same
// value is a decimal, though no type source is there to give the type index a place for it.
public sealed class DecimalReadPlacesTests
{
    private const string NotAccessible = "is not available to expressions: no type source of the environment describes it";

    private static readonly CelObjectType Box = CelObjectTypeBuilder.ForDictionary("acme.Box")
        .AddField("value", CelType.Dyn)
        .AddField("title", CelType.String)
        .Build();

    private const string Rows = $$"""
        Evaluate | box^.value | TypeNotAccessible | .NET type 'System.Decimal' {{NotAccessible}}
        Evaluate | give^() | TypeNotAccessible | .NET type 'System.Decimal' {{NotAccessible}}
        Evaluate | l.exists(x, x ^+ 1 > 0) | EvalNoMatchingOverload | -
        """;

    // The title of the same object is read as ever, and has() tests the presence of the value without reading it.
    private const string Reads = """
        box.title => 't'
        has(box.value) => true
        """;

    private static readonly IReadOnlyDictionary<string, object?> Contents = new Dictionary<string, object?>
    {
        ["value"] = 1.5m,
        ["title"] = "t",
    };

    [Fact]
    public static void DecimalInAFieldOfAnObjectAndInTheResultOfAFunction_IsTypeNotAccessibleWithoutTheSet()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddTypes(CelTypeProvider.Create("boxes", Box))
            .AddVariable("box", Box.Type)
            .AddVariable("l", CelType.ListOf(CelType.Dyn))
            .AddFunction<object>("give", () => 1.5m)
            .Build()
            .GetEnvironmentOrThrow();
        CelBindings bindings = new CelBindings()
            .Add("box", Contents)
            .Add<IReadOnlyList<object?>>("l", new object?[] { 2.5m });

        Assert.Empty(ExtensionRows.CheckErrors(environment, Rows, bindings));
        Assert.Empty(ExtensionRows.CheckValues(environment, Reads, bindings));
    }

#if NET11_0_OR_GREATER
    // The set is what makes a decimal usable: with it the dyn values of the application are decimals, in a place that a type source
    // gives and in one that none does.
    [Fact]
    public static void DecimalInADynPlace_IsADecimalWithTheSet()
    {
        CelBindings bindings = new CelBindings()
            .Add("box", Contents)
            .Add<object>("d", DecimalExample.Make(15, -1))
            .Add<IReadOnlyList<object?>>("l", new object?[] { DecimalExample.Make(25, -1) });
        CelEnvironment withTypes = DecimalExample.Builder()
            .AddTypes(CelTypeProvider.Create("boxes", Box))
            .AddVariable("box", Box.Type)
            .Build()
            .GetEnvironmentOrThrow();
        CelEnvironment withoutTypes = DecimalExample.Builder()
            .AddVariable("d", CelType.Dyn)
            .AddVariable("l", CelType.ListOf(CelType.Dyn))
            .Build()
            .GetEnvironmentOrThrow();

        Assert.Empty(ExtensionRows.CheckValues(withTypes, "string(box.value) => '1.5'", bindings));
        Assert.Empty(ExtensionRows.CheckValues(
            withoutTypes,
            """
            string(d) => '1.5'
            string(l[0]) => '2.5'
            d + l[0] == 4m => true
            [d].exists(x, x == 1.5m) => true
            """,
            bindings));
    }

    // A literal needs neither the checker nor the functions of the set: the plan of a text that was only parsed has the constant of the
    // kind Decimal, with the digits, the exponent and the sign that the text gives.
    [Fact]
    public static void LiteralWithoutTypeChecking_IsAConstantOfTheKindDecimal()
    {
        CelEnvironment environment = DecimalExample.Create();
        ParseResult parsed = environment.Parse("-1.50m");
        Assert.True(parsed.IsSuccess);

        CelValue value = Planner.PlanWithoutTypeCheck(parsed.Expression, environment.Declarations).EvaluateOn(new Dictionary<string, CelValue>());

        Assert.True(value.TryGetDecimal(out Decimal128 number));
        Assert.Equal((true, (System.UInt128)150, -2), DecimalExample.Parts(number));
    }
#endif
}

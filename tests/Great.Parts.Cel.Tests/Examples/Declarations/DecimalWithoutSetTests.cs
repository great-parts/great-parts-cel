using System.Collections.Generic;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Examples.Library;
using Great.Parts.Cel.Types;
using Xunit;
#if NET11_0_OR_GREATER
using System.Numerics;
#endif

namespace Great.Parts.Cel.Tests.Examples.Declarations;

// The environment without the set decimal, in both targets (the page of the extension set decimal, "An environment without the
// set"): the class of the outcome is the same in net11.0 and in net10.0 — an error that names what is missing — and the codes are
// the same except where net10.0 has no type at all. In net10.0 there is no set, no literal option and no decimal type; the
// examples that use them are of net11.0 alone, and this file is the one that is of both.
//   12.34m and 100m are SyntaxNotEnabled (1007) at the literal, with the text that names the option in net11.0 and the text that
//   says decimal needs .NET 11 in net10.0; decimal('1') and type(x) == decimal are UndeclaredReference (2000), at the "(" of the call
//   and at the name, because nothing declares the function and the identifier; a variable of the type System.Decimal (and, in
//   net11.0, of Decimal128) is TypeNotDeclared (4008) at Build, whose hint names the set in net11.0 and says that the set exists
//   only on .NET 11 in net10.0; a value System.Decimal that the application puts in a place of the type dyn is TypeNotAccessible
//   (3012) at the node that reads it, with the name of the .NET type, in both targets, since in net10.0 it is an object that no type
//   source describes and in net11.0 it is a decimal that the environment cannot have. (The member of an object type that is a
//   decimal is FieldNotAccessible, DecimalFieldCheckTests.) Rows are those of ExtensionRows; d is a variable of the type dyn, l of
//   list(dyn).
[Trait("Requirement", "R-5")]
[Trait("Requirement", "F-5")]
public sealed class DecimalWithoutSetTests
{
#if NET11_0_OR_GREATER
    private const string SyntaxHint = "enable decimal literals in the environment builder";
    private const string VariableHint = "; add the extension set 'decimal'";
    private const string VariableType = "decimal";
#else
    private const string SyntaxHint = "decimal requires .NET 11";
    private const string VariableHint = "; the extension set 'decimal' exists only on .NET 11";
    private const string VariableType = "System.Decimal";
#endif

    private const string NotAccessible = "is not available to expressions: no type source of the environment describes it";

    private const string Literals = $$"""
        Parse | ^12.34m | SyntaxNotEnabled | decimal literal is not enabled: {{SyntaxHint}}
        Parse | ^100m | SyntaxNotEnabled | decimal literal is not enabled: {{SyntaxHint}}
        Parse | ^0m | SyntaxNotEnabled | decimal literal is not enabled: {{SyntaxHint}}
        Parse | ^100M | SyntaxNotEnabled | decimal literal is not enabled: {{SyntaxHint}}
        Parse | ^1e2m | SyntaxNotEnabled | decimal literal is not enabled: {{SyntaxHint}}
        Parse | ^.5m | SyntaxNotEnabled | decimal literal is not enabled: {{SyntaxHint}}
        Parse | ^-100m | SyntaxNotEnabled | decimal literal is not enabled: {{SyntaxHint}}
        Parse | 1 + ^100m + ^12.34m | SyntaxNotEnabled SyntaxNotEnabled | decimal literal is not enabled: {{SyntaxHint}} ;; decimal literal is not enabled: {{SyntaxHint}}
        """;

    private const string Names = """
        Check | decimal^('1') | UndeclaredReference | undeclared reference to 'decimal' (in container '')
        Check | decimal^(1.5) | UndeclaredReference | undeclared reference to 'decimal' (in container '')
        Check | type(x) == ^decimal | UndeclaredReference | undeclared reference to 'decimal' (in container '')
        Check | ^decimal | UndeclaredReference | undeclared reference to 'decimal' (in container '')
        """;

    private const string DynPlaces = $$"""
        Evaluate | ^d | TypeNotAccessible | .NET type 'System.Decimal' {{NotAccessible}}
        Evaluate | ^d == 1 | TypeNotAccessible | .NET type 'System.Decimal' {{NotAccessible}}
        Evaluate | l^[0] | TypeNotAccessible | .NET type 'System.Decimal' {{NotAccessible}}
        """;

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable("x", CelType.Dyn)
        .AddVariable("d", CelType.Dyn)
        .AddVariable("l", CelType.ListOf(CelType.Dyn))
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    [Trait("Requirement", "F-4")]
    [Trait("Requirement", "F-8")]
    public void LiteralsOfBothForms_AreSyntaxNotEnabled() => Assert.Empty(ExtensionRows.CheckErrors(environment, Literals));

    [Fact]
    [Trait("Requirement", "F-4")]
    public void FunctionAndTypeName_AreUndeclaredReference() => Assert.Empty(ExtensionRows.CheckErrors(environment, Names));

    [Fact]
    [Trait("Requirement", "F-4")]
    [Trait("Requirement", "F-8")]
    public void VariableOfTheTypeSystemDecimal_IsTypeNotDeclaredWithTheHintOfTheTarget()
    {
        CelBuildResult result = new CelEnvironmentBuilder(CelStandardLibrary.V1).AddVariable<decimal>("price").Build();

        Assert.False(result.IsSuccess);
        CelError error = Assert.Single(result.Errors);
        Assert.Equal(
            (CelErrorCode.TypeNotDeclared, CelErrorStage.Build, "variable 'price' has type '" + VariableType + "', which no type source or extension of the environment declares" + VariableHint),
            (error.Code, error.Stage, error.Message));
        Assert.Equal(default(CelSourceLocation), error.Location);
    }

    [Fact]
    [Trait("Requirement", "F-4")]
    public void NullableSystemDecimal_IsTypeNotDeclaredToo()
    {
        CelBuildResult result = new CelEnvironmentBuilder(CelStandardLibrary.V1).AddVariable<decimal?>("price").Build();

        Assert.False(result.IsSuccess);
        Assert.Equal(CelErrorCode.TypeNotDeclared, Assert.Single(result.Errors).Code);
    }

    // The value is there at the first read of the variable, and at the read of an element: the node that reads it is the place.
    [Fact]
    [Trait("Requirement", "F-2")]
    [Trait("Requirement", "F-4")]
    public void SystemDecimalInAPlaceOfTypeDyn_IsTypeNotAccessibleAtTheNodeThatReadsIt()
    {
        CelBindings bindings = new CelBindings()
            .Add<object>("d", 1.5m)
            .Add<IReadOnlyList<object?>>("l", new object?[] { 2.5m });

        Assert.Empty(ExtensionRows.CheckErrors(environment, DynPlaces, bindings));
    }

#if NET11_0_OR_GREATER
    // A Decimal128 is the type of net11.0 alone; it is the same TypeNotDeclared, and a Decimal128 in a place of the type dyn is the
    // same TypeNotAccessible with its own name.
    [Fact]
    [Trait("Requirement", "F-4")]
    public void Decimal128WithoutTheSet_IsTypeNotDeclaredAndTypeNotAccessible()
    {
        CelBuildResult result = new CelEnvironmentBuilder(CelStandardLibrary.V1).AddVariable<Decimal128>("price").Build();

        Assert.False(result.IsSuccess);
        CelError error = Assert.Single(result.Errors);
        Assert.Equal(
            (CelErrorCode.TypeNotDeclared, CelErrorStage.Build, "variable 'price' has type 'decimal', which no type source or extension of the environment declares; add the extension set 'decimal'"),
            (error.Code, error.Stage, error.Message));

        CelBindings bindings = new CelBindings().Add<object>("d", DecimalExample.Make(15, -1));
        const string Rows = "Evaluate | ^d | TypeNotAccessible | .NET type 'System.Numerics.Decimal128' " + NotAccessible;

        Assert.Empty(ExtensionRows.CheckErrors(environment, Rows, bindings));
    }

    // The literal, the function and the member need the set and, for the literal, the option; the set alone gives the function but
    // not the literal, and the option alone gives nothing (DecimalSetBuildTests).
    [Fact]
    [Trait("Requirement", "F-5")]
    public void SetWithoutTheOption_GivesTheFunctionAndNotTheLiteral()
    {
        CelEnvironment withSet = new CelEnvironmentBuilder(CelStandardLibrary.V1).AddExtension(CelExtensions.Decimal(1)).Build().GetEnvironmentOrThrow();

        Assert.True(withSet.Compile<Great.Parts.Cel.Values.CelValue>("decimal('12.34') + decimal('1')").IsSuccess);
        Assert.False(withSet.Compile<Great.Parts.Cel.Values.CelValue>("12.34m").IsSuccess);
    }
#endif
}

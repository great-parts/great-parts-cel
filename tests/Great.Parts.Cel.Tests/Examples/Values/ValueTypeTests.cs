using System.Collections.Generic;
using System.Threading;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Values;

// The runtime type of a value, which the function type returns: the parameters of lists and maps are erased,
// and a type value has the type type(dyn). And whether a value fits a declared type at the top level: dyn
// takes any value, a primitive type a value of its kind, wrapper(P) null or a value of P, list(…) a list and
// map(…) a map, type(…) any type value. Evaluation checks bindings and results of application functions to
// the full depth; the examples of this file state only the top level.
public sealed class ValueTypeTests
{
    private static readonly CelFunctionImplementation ReturnsText = _ => CelValue.FromString("no");

    private static readonly CelFunctionImplementation ReturnsNull = _ => CelValue.Null;

    private readonly CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddVariable("i", CelType.Int)
        .AddVariable("l", CelType.ListOf(CelType.Int))
        .AddVariable("m", CelType.MapOf(CelType.String, CelType.Int))
        .AddVariable("w", CelType.WrapperOf(CelType.Int))
        .AddVariable("t", CelType.TypeOf(CelType.Dyn))
        .AddVariable("d", CelType.Dyn)
        .AddVariable("ld", CelType.ListOf(CelType.Dyn))
        .AddVariable("ms", CelType.MapOf(CelType.String, CelType.Dyn))
        .AddVariable("ts", CelType.TypeOf(CelType.String))
        .AddFunction(new CelFunctionDecl("answer", CelOverloadDecl.Global("answer_string", [CelType.String], CelType.Int)))
        .AddFunction(new CelFunctionDecl("anything", CelOverloadDecl.Global("anything_int", [CelType.Int], CelType.Dyn)))
        .AddFunction(new CelFunctionDecl("maybe", CelOverloadDecl.Global("maybe_int", [CelType.Int], CelType.WrapperOf(CelType.Int))))
        .AddFunctionImplementation("answer_string", ReturnsText)
        .AddFunctionImplementation("anything_int", ReturnsText)
        .AddFunctionImplementation("maybe_int", ReturnsNull)
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    [Trait("Requirement", "F-1")]
    public void TypeOfAValue_IsItsRuntimeTypeWithErasedParameters()
    {
        Assert.Equal(CelType.Int, Evaluate<CelType>("type(1)", new CelBindings()));
        Assert.Equal(CelType.UInt, Evaluate<CelType>("type(dyn(1u))", new CelBindings()));
        Assert.Equal(CelType.Double, Evaluate<CelType>("type(1.5)", new CelBindings()));
        Assert.Equal(CelType.String, Evaluate<CelType>("type('a')", new CelBindings()));
        Assert.Equal(CelType.Bytes, Evaluate<CelType>("type(b'a')", new CelBindings()));
        Assert.Equal(CelType.Bool, Evaluate<CelType>("type(true)", new CelBindings()));
        Assert.Equal(CelType.Null, Evaluate<CelType>("type(null)", new CelBindings()));
        Assert.Equal(CelType.ListOf(CelType.Dyn), Evaluate<CelType>("type([1])", new CelBindings()));
        Assert.Equal(CelType.ListOf(CelType.Dyn), Evaluate<CelType>("type(dyn([1]))", new CelBindings()));
        Assert.Equal(CelType.MapOf(CelType.Dyn, CelType.Dyn), Evaluate<CelType>("type({'a': 1})", new CelBindings()));
        Assert.Equal(CelType.TypeOf(CelType.Dyn), Evaluate<CelType>("type(int)", new CelBindings()));
    }

    // A type identifier is a constant type value of its type; list and map denote list(dyn) and map(dyn, dyn).
    [Fact]
    [Trait("Requirement", "F-1")]
    public void TypeIdentifier_IsATypeValue()
    {
        Assert.Equal(CelType.Int, Evaluate<CelType>("int", new CelBindings()));
        Assert.Equal(CelType.Null, Evaluate<CelType>("null_type", new CelBindings()));
        Assert.Equal(CelType.ListOf(CelType.Dyn), Evaluate<CelType>("list", new CelBindings()));
        Assert.Equal(CelType.MapOf(CelType.Dyn, CelType.Dyn), Evaluate<CelType>("map", new CelBindings()));
        Assert.Equal(CelType.TypeOf(CelType.Dyn), Evaluate<CelType>("type", new CelBindings()));
    }

    [Fact]
    [Trait("Requirement", "F-3")]
    public void BindingThatFitsTheDeclaredType_IsRead()
    {
        var bindings = new CelBindings()
            .Add("i", 41L)
            .Add("l", CelList.Create(CelValue.FromInt64(1)))
            .Add("m", CelMap.Create([]))
            .Add("w", CelValue.Null)
            .Add("t", CelValue.FromType(CelType.Int))
            .Add("d", "text")
            .Add("ld", Mixed())
            .Add("ms", StringKeyed(CelValue.FromInt64(1)))
            .Add("ts", CelValue.FromType(CelType.Int));

        Assert.True(Evaluate<bool>("i == 41 && size(l) == 1 && size(m) == 0 && w == null && t == int && d == 'text'", bindings));
        Assert.True(Evaluate<bool>("size(ld) == 2 && ms['a'] == 1 && ts == int", bindings));
        Assert.Equal(5L, Evaluate<long?>("w", new CelBindings().Add("w", 5L)));
    }

    // The message names the variable, the runtime type of the value and the declared type; the place is the
    // identifier.
    [Theory]
    [Trait("Requirement", "F-3")]
    [Trait("Requirement", "F-4")]
    [InlineData("1 + i", "i", 4, "string", "int")]
    [InlineData("size(l)", "l", 5, "int", "list(int)")]
    [InlineData("size(m)", "m", 5, "list", "map(string, int)")]
    [InlineData("[w]", "w", 1, "string", "wrapper(int)")]
    [InlineData("[t]", "t", 1, "int", "type(dyn)")]
    public void BindingOfAnotherKind_IsBindingTypeMismatchAtTheIdentifier(string expression, string name, int offset, string actual, string declared)
    {
        var bindings = new CelBindings()
            .Add("i", "text")
            .Add("l", 1L)
            .Add("m", CelList.Create())
            .Add("w", "text")
            .Add("t", 1L);

        CelError error = Assert.Single(Run<CelValue>(expression, bindings).Errors);

        Assert.Equal((CelErrorCode.BindingTypeMismatch, CelErrorStage.Evaluate, offset), (error.Code, error.Stage, error.Location.Offset));
        Assert.Equal(
            "value of variable '" + name + "' has type '" + actual + "', which does not match the declared type '" + declared + "'",
            error.Message);
    }

    // A result of an application function must fit the declared result type, unless that type is dyn; the
    // place is the call, its "(".
    [Fact]
    [Trait("Requirement", "F-3")]
    [Trait("Requirement", "F-4")]
    public void ResultOfAnotherKind_IsImplementationResultMismatchAtTheCall()
    {
        CelError error = Assert.Single(Run<CelValue>("answer('q')", new CelBindings()).Errors);

        Assert.Equal((CelErrorCode.ImplementationResultMismatch, CelErrorStage.Evaluate, 6), (error.Code, error.Stage, error.Location.Offset));
        Assert.Equal(
            "implementation of overload 'answer_string' of function 'answer' returned a value of type 'string', expected 'int'",
            error.Message);
        Assert.Equal("no", Evaluate<string>("anything(1)", new CelBindings()));
        Assert.Equal(CelValueKind.Null, Evaluate<CelValue>("maybe(1)", new CelBindings()).Kind);
    }

    // [1, 'a']
    private static CelList Mixed() => CelList.Create(CelValue.FromInt64(1), CelValue.FromString("a"));

    // {'a': value}
    private static CelMap StringKeyed(CelValue value) => CelMap.Create([new KeyValuePair<CelValue, CelValue>(CelValue.FromString("a"), value)]);

    private CelEvalResult<T> Run<T>(string expression, CelBindings bindings) =>
        environment.Compile<T>(expression).GetProgramOrThrow().Evaluate(bindings, cancellationToken: CancellationToken.None);

    private T Evaluate<T>(string expression, CelBindings bindings) => Run<T>(expression, bindings).GetValueOrThrow();
}

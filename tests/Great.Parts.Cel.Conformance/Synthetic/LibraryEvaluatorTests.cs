using System;
using System.Collections.Generic;
using Cel.Expr;
using Great.Parts.Cel.Conformance.Runner;
using Great.Parts.Cel.Types;
using Xunit;
using Type = Cel.Expr.Type;

namespace Great.Parts.Cel.Conformance.Synthetic;

// The environment of a case (docs/design/conformance.md, "Environment of the cases"). The flag disable_macros: the paths through
// the internal entry points parse without macros, as the cel-go runner clears them; the checked path has no entry point with syntax
// options. Functions of type_env: one internal set with a stub for each overload and an abstract type for each name of the
// declarations (docs/design/conformance.md, "The sets optional and block in the runner").
public sealed class LibraryEvaluatorTests
{
    // With the macros, a call of all whose first argument is not a name is a parse error; without them it is a call.
    private const string InvalidMacroCall = "[1].all(1, true)";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public static void CaseWithDisableMacros_IsParsedWithoutMacros(bool checkOnly)
    {
        CheckMode check = checkOnly ? CheckMode.CheckOnly : CheckMode.WithoutCheck;
        CaseOutcome expanded = Evaluate(check, disableMacros: false);
        CaseOutcome call = Evaluate(check, disableMacros: true);

        Assert.Equal(CaseStage.Parse, expanded.Stage);
        Assert.Equal(["invalid call of macro 'all': argument must be a simple name"], expanded.Errors);
        Assert.Null(call.Exception);
        Assert.Equal(checkOnly ? CaseStage.Check : CaseStage.Evaluate, call.Stage);
        Assert.NotEmpty(call.Errors);
    }

    [Fact]
    public void CheckedCase_WithDisableMacros_FailsWithTheReason()
    {
        CaseOutcome outcome = Evaluate(CheckMode.Check, disableMacros: true);

        Assert.Equal(CaseStage.Parse, outcome.Stage);
        Assert.Equal([LibraryEvaluator.DisableMacrosOnCheckedPath], outcome.Errors);
    }

    // A function of type_env is declared with a stub for its implementation, which Build requires: the case that only checks types gets
    // the type of the result, in every mode of the case that reaches the check.
    [Fact]
    public void CheckOnlyCase_WithAFunction_DeducesTheResultType()
    {
        Decl function = Function("fn", Overload("fn_string_int", Primitive(Type.Types.PrimitiveType.String), Primitive(Type.Types.PrimitiveType.String), Primitive(Type.Types.PrimitiveType.Int64)));

        CaseOutcome outcome = Evaluate(CheckMode.CheckOnly, disableMacros: false, "fn('abc', 123)", [function]);

        Assert.Null(outcome.Exception);
        Assert.Equal((CaseStage.Check, Primitive(Type.Types.PrimitiveType.String)), (outcome.Stage, outcome.ResultType));
    }

    // The overload has type parameters and its result is an abstract type of three parameters, which no public member declares: the
    // runner declares the name once, however often the cases write it, and the parameters are substituted at the call.
    [Fact]
    public void CheckOnlyCase_WithTypeParametersAndAnAbstractType_DeducesTheSubstitutedType()
    {
        Type tuple = Abstract("tuple", Parameter("T"), Parameter("U"), Parameter("V"));
        Decl constructor = Function("tuple", Overload("tuple_T_U_V", tuple, Parameter("T"), Parameter("U"), Parameter("V")));
        Decl sort = Function("sort", Overload("sort_tuple_T_T_T", Abstract("tuple", Parameter("T"), Parameter("T"), Parameter("T")), Abstract("tuple", Parameter("T"), Parameter("T"), Parameter("T"))));

        CaseOutcome outcome = Evaluate(CheckMode.CheckOnly, disableMacros: false, "tuple(1, 2u, 3.0)", [constructor, sort]);

        Assert.Null(outcome.Exception);
        Assert.Equal(
            Abstract("tuple", Primitive(Type.Types.PrimitiveType.Int64), Primitive(Type.Types.PrimitiveType.Uint64), Primitive(Type.Types.PrimitiveType.Double)),
            outcome.ResultType);
    }

    // A case that evaluates what it declared does not pass: the stub throws, which the library reports as an error of the call.
    [Fact]
    public void CaseThatEvaluatesAStub_FailsWithAnErrorOfTheCall()
    {
        Decl function = Function("fn", Overload("fn_int", Primitive(Type.Types.PrimitiveType.Int64), Primitive(Type.Types.PrimitiveType.Int64)));

        CaseOutcome outcome = Evaluate(CheckMode.Check, disableMacros: false, "fn(1)", [function]);

        Assert.Null(outcome.Value);
        Assert.Equal(CaseStage.Evaluate, outcome.Stage);
        Assert.Equal(["application code threw System.InvalidOperationException in overload 'fn_int' of function 'fn'"], outcome.Errors);
    }

    // The type of the set optional is no type of the runner's own: a declaration that writes optional_type builds with the set that
    // declares it, and the name is not declared twice.
    [Fact]
    public void DeclarationOfOptionalType_UsesTheSetThatDeclaresIt()
    {
        Type optionalInt = Abstract("optional_type", Primitive(Type.Types.PrimitiveType.Int64));
        Decl function = Function("fn", Overload("fn_optional", optionalInt, optionalInt));
        Decl identifier = new() { Name = "x", Ident = new Decl.Types.IdentDecl { Type = optionalInt } };

        CaseOutcome outcome = Evaluate(CheckMode.CheckOnly, disableMacros: false, "fn(x)", [function, identifier]);

        Assert.Null(outcome.Exception);
        Assert.Equal((CaseStage.Check, optionalInt), (outcome.Stage, outcome.ResultType));
    }

    // Every case has the sets optional and block with bindings: the optional syntax, the macros of the test library and the variables
    // @index0 to @index29, which a case that is not about blocks never names.
    [Fact]
    public void EveryCase_HasTheSetsOptionalAndBlock()
    {
        CaseOutcome optional = Evaluate(CheckMode.Check, disableMacros: false, "optional.of(1).orValue(2) + [?optional.of(5)].size()", []);
        CaseOutcome block = Evaluate(CheckMode.Check, disableMacros: false, "cel.block([1, cel.index(0) + 1], cel.index(1))", []);

        Assert.Equal(2, optional.Value?.Int64Value);
        Assert.Equal(2, block.Value?.Int64Value);
    }

    // A type of a result that is optional_type(T) is the abstract type of that name, and a count of parameters that the library has no
    // type for is not converted.
    [Fact]
    public void TypeConversion_OfOptionalType_ReadsBothWays()
    {
        Type optionalString = Abstract("optional_type", Primitive(Type.Types.PrimitiveType.String));

        Assert.Equal(CelType.OptionalOf(CelType.String), TypeConversion.ToCelType(optionalString));
        Assert.Equal(optionalString, TypeConversion.ToExprType(CelType.OptionalOf(CelType.String)));
        Assert.Throws<NotImplementedException>(() => TypeConversion.ToCelType(Abstract("optional_type")));
        Assert.Throws<NotImplementedException>(() => TypeConversion.ToCelType(Abstract("optional_type", Primitive(Type.Types.PrimitiveType.Int64), Primitive(Type.Types.PrimitiveType.Int64))));
    }

    // Another abstract type is read by name with its parameters; a type parameter is a parameter of the declaration only.
    [Fact]
    public void TypeConversion_OfAnAbstractTypeAndOfATypeParameter()
    {
        Type pair = Abstract("pair", Primitive(Type.Types.PrimitiveType.Bool), Parameter("T"));

        CelType converted = TypeConversion.ToCelType(pair);

        Assert.Equal(CelTypeKind.Abstract, converted.Kind);
        Assert.Equal("pair", converted.Name);
        Assert.Equal(2, converted.Parameters.Length);
        Assert.Equal(CelType.Bool, converted.Parameters[0]);
        Assert.Equal(CelType.TypeParameter("T"), converted.Parameters[1]);
    }

    private static Type Primitive(Type.Types.PrimitiveType primitive) => new() { Primitive = primitive };

    private static Type Parameter(string name) => new() { TypeParam = name };

    private static Type Abstract(string name, params Type[] parameters) =>
        new() { AbstractType = new Type.Types.AbstractType { Name = name, ParameterTypes = { parameters } } };

    private static Decl.Types.FunctionDecl.Types.Overload Overload(string id, Type result, params Type[] parameters) =>
        new() { OverloadId = id, ResultType = result, Params = { parameters } };

    private static Decl Function(string name, params Decl.Types.FunctionDecl.Types.Overload[] overloads) =>
        new() { Name = name, Function = new Decl.Types.FunctionDecl { Overloads = { overloads } } };

    private static CaseOutcome Evaluate(CheckMode check, bool disableMacros) => Evaluate(check, disableMacros, InvalidMacroCall, []);

    private static CaseOutcome Evaluate(CheckMode check, bool disableMacros, string expression, IReadOnlyList<Decl> declarations) =>
        new LibraryEvaluator().Evaluate(new ConformanceCase(
            "f/s/t",
            "f.textproto",
            1,
            1,
            expression,
            new CaseEnvironment("", declarations, disableMacros),
            new Dictionary<string, ExprValue>(),
            check,
            new Expectation(ExpectationKind.Value, "none", new Value { BoolValue = true }, null)));
}

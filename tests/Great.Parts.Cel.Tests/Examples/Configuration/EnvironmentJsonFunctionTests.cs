using System;
using System.Threading;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Configuration;

// The key functions of the environment description in JSON (the page of the description, "Mapping onto builder
// calls"): a function has a name and a non-empty array of overloads, each with an id, an optional target and
// args, a return type and examples (metadata). A function is AddFunction(CelFunctionDecl) with its overloads
// in the order of the array and without implementations, which the application registers in code with
// AddFunctionImplementation by the identifier of the overload, before or after Apply. What the builder
// reports at Build is not an error of the description: Apply returns none. In the marked documents each '|' is
// the place of one expected error (see JsonExample).
[Trait("Requirement", "F-7")]
[Trait("Requirement", "F-3")]
public sealed class EnvironmentJsonFunctionTests
{
    private static readonly CelFunctionImplementation Echo = arguments => arguments[0];

    private const string Discount = """
        {
          "functions": [{
            "name": "discount",
            "description": "A discount.",
            "overloads": [
              { "id": "discount_int_double", "args": ["int", "double"], "return": "double", "examples": ["discount(1, 2.0)"] },
              { "id": "int_discount", "target": "int", "args": ["double"], "return": "double" },
              { "id": "now_discount", "return": "int" },
              { "id": "first_list", "args": ["list<~A>"], "return": "~A" },
              { "id": "map_pair", "target": "map<~k, ~V>", "args": [" ~V"], "return": "list<~k>" }
            ]
          }]
        }
        """;

    [Fact]
    public void Function_IsTheDeclarationWithItsOverloadsInOrder()
    {
        CelEnvironmentBuilder inCode = JsonExample.NewBuilder().AddFunction(new CelFunctionDecl(
            "discount",
            CelOverloadDecl.Global("discount_int_double", [CelType.Int, CelType.Double], CelType.Double),
            CelOverloadDecl.Receiver("int_discount", CelType.Int, [CelType.Double], CelType.Double),
            CelOverloadDecl.Global("now_discount", [], CelType.Int),
            CelOverloadDecl.Global("first_list", [CelType.ListOf(CelType.TypeParameter("A"))], CelType.TypeParameter("A")),
            CelOverloadDecl.Receiver(
                "map_pair",
                CelType.MapOf(CelType.TypeParameter("k"), CelType.TypeParameter("V")),
                [CelType.TypeParameter("V")],
                CelType.ListOf(CelType.TypeParameter("k")))));
        var builder = JsonExample.NewBuilder();

        Assert.Empty(JsonExample.Apply(builder, Discount));

        Assert.Equal(JsonExample.Functions(inCode), JsonExample.Functions(builder));
    }

    // The implementations are the application's, by the identifier of the overload; before or after Apply the
    // environment is the same.
    [Fact]
    public void ImplementationRegisteredBeforeOrAfterApply_IsBoundByTheIdentifier()
    {
        const string Json = """
            {"functions": [{"name": "twice", "overloads": [
              {"id": "twice_int", "args": ["int"], "return": "int"},
              {"id": "int_twice", "target": "int", "return": "int"}
            ]}]}
            """;
        var before = JsonExample.NewBuilder()
            .AddFunctionImplementation<long, long>("twice_int", number => number * 2)
            .AddFunctionImplementation<long, long>("int_twice", number => number * 2);
        Assert.Empty(JsonExample.Apply(before, Json));
        var after = JsonExample.NewBuilder();
        Assert.Empty(JsonExample.Apply(after, Json));
        after
            .AddFunctionImplementation<long, long>("int_twice", number => number * 2)
            .AddFunctionImplementation<long, long>("twice_int", number => number * 2);

        foreach (CelEnvironmentBuilder builder in new[] { before, after })
        {
            CelEnvironment environment = builder.Build().GetEnvironmentOrThrow();

            Assert.Equal(42L, Evaluate(environment, "twice(21)"));
            Assert.Equal(42L, Evaluate(environment, "21.twice()"));
        }
    }

    // A full repeat of an overload (the same name, identifier and signature) is one declaration.
    [Fact]
    public void FullRepeatOfAnOverload_IsOneDeclaration()
    {
        const string Json = """
            {"functions": [
              {"name": "twice", "overloads": [{"id": "twice_int", "args": ["int"], "return": "int"}]},
              {"name": "twice", "overloads": [{"id": "twice_int", "args": ["int"], "return": "int"}]}
            ]}
            """;
        var builder = JsonExample.NewBuilder().AddFunctionImplementation<long, long>("twice_int", number => number * 2);
        Assert.Empty(JsonExample.Apply(builder, Json));

        Assert.Equal(42L, Evaluate(builder.Build().GetEnvironmentOrThrow(), "twice(21)"));
    }

    // A missing name and a missing return are errors at the "{" of the object, and so are an empty array of
    // overloads and a missing array, which the constructor of the declaration rejects.
    [Theory]
    [InlineData("""{"functions": [|{"overloads": [{"id": "a", "return": "int"}]}]}""")]
    [InlineData("""{"functions": [|{"name": "f"}]}""")]
    [InlineData("""{"functions": [|{"name": "f", "overloads": []}]}""")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [|{"return": "int"}]}]}""")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [|{"id": "a"}]}]}""")]
    public void MissingKey_IsAnErrorAtTheObject(string marked) =>
        JsonExample.AssertError(marked);

    // An empty name or id, a value or an element of the wrong kind and a wrong type record are errors at the value.
    [Theory]
    [InlineData("""{"functions": [{"name": |"", "overloads": [{"id": "a", "return": "int"}]}]}""")]
    [InlineData("""{"functions": [{"name": |1, "overloads": [{"id": "a", "return": "int"}]}]}""")]
    [InlineData("""{"functions": [{"name": "f", "description": |1, "overloads": [{"id": "a", "return": "int"}]}]}""")]
    [InlineData("""{"functions": [{"name": "f", "overloads": |{}}]}""")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [|1]}]}""")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [{"id": |"", "return": "int"}]}]}""")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [{"id": |1, "return": "int"}]}]}""")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [{"id": "a", "return": |1}]}]}""")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [{"id": "a", "return": |"list<>"}]}]}""")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [{"id": "a", "return": "int", "target": |"~"}]}]}""")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [{"id": "a", "return": "int", "target": |["int"]}]}]}""")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [{"id": "a", "return": "int", "args": |"int"}]}]}""")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [{"id": "a", "return": "int", "args": ["int", |1]}]}]}""")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [{"id": "a", "return": "int", "args": ["int", |"list<int, int>"]}]}]}""")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [{"id": "a", "return": "int", "examples": |"f()"}]}]}""")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [{"id": "a", "return": "int", "examples": [|1]}]}]}""")]
    public void EmptyNameOrValueOfTheWrongKind_IsAnErrorAtTheValue(string marked) =>
        JsonExample.AssertError(marked);

    [Theory]
    [InlineData("""{"functions": [{"name": "f", |"doc": "d", "overloads": [{"id": "a", "return": "int"}]}]}""")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [{"id": "a", "return": "int", |"cost": 1}]}]}""")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [{"id": "a", "return": "int", |"id": "b"}]}]}""")]
    [InlineData("""{"functions": [{"name": "f", |"name": "g", "overloads": [{"id": "a", "return": "int"}]}]}""")]
    public void UnknownOrRepeatedKey_IsAnErrorAtTheKey(string marked) =>
        JsonExample.AssertError(marked);

    // A type parameter belongs to an overload: it is right in target, args and return.
    [Fact]
    public void TypeParameter_IsRightInAnOverload()
    {
        var builder = JsonExample.NewBuilder();

        Assert.Empty(JsonExample.Apply(
            builder,
            """{"functions": [{"name": "f", "overloads": [{"id": "f_list", "target": "list<~A>", "args": ["~A"], "return": "~A"}]}]}"""));
    }

    // The name of a function that is no identifier or qualified name, here the name of an operator, is
    // InvalidFunctionName at Build, with the label of the description and the place of the function.
    [Fact]
    public void NameOfAnOperator_IsInvalidFunctionNameAtBuild()
    {
        const string Json = """{"name": "m", "functions": [{"name": "_+_", "overloads": [{"id": "plus_int", "args": ["int", "int"], "return": "int"}]}]}""";
        var builder = JsonExample.NewBuilder();
        Assert.Empty(JsonExample.Apply(builder, Json));

        CelError error = JsonExample.BuildError(builder);

        JsonExample.AssertBuildErrorAt(
            error,
            CelErrorCode.InvalidFunctionName,
            "function name '_+_' of environment JSON 'm' is not an identifier or a qualified name; operators and internal functions cannot be declared",
            "environment JSON 'm'",
            Json.IndexOf("{\"name\": \"_+_\"", StringComparison.Ordinal));
    }

    // An overload without an implementation is MissingImplementation at Build. The text of the error names no
    // source, yet the error carries the place of the overload in the JSON, with the label as the source name.
    [Fact]
    public void OverloadWithoutAnImplementation_IsMissingImplementationAtBuild()
    {
        const string Json = """{"name": "m", "functions": [{"name": "twice", "overloads": [{"id": "twice_int", "args": ["int"], "return": "int"}]}]}""";
        var builder = JsonExample.NewBuilder();
        Assert.Empty(JsonExample.Apply(builder, Json));

        JsonExample.AssertBuildErrorAt(
            JsonExample.BuildError(builder),
            CelErrorCode.MissingImplementation,
            "overload 'twice_int' of function 'twice' has no implementation",
            "environment JSON 'm'",
            Json.IndexOf("{\"id\": \"twice_int\"", StringComparison.Ordinal));
    }

    // An implementation whose identifier no declaration has is UnusedImplementation, and a typed delegate that
    // does not fit the declared signature is ImplementationSignatureMismatch. The first has no place: no JSON
    // declaration has the identifier. The second carries the place of the overload in the JSON.
    [Fact]
    public void ImplementationThatFitsNothing_IsAnErrorAtBuild()
    {
        const string Json = """{"name": "m", "functions": [{"name": "twice", "overloads": [{"id": "twice_int", "args": ["int"], "return": "int"}]}]}""";
        var unused = JsonExample.NewBuilder().AddFunctionImplementation<long, long>("twice_int", number => number * 2)
            .AddFunctionImplementation<long, long>("nothing_int", number => number);
        var mismatch = JsonExample.NewBuilder().AddFunctionImplementation<string, bool>("twice_int", text => text.Length > 0);
        Assert.Empty(JsonExample.Apply(unused, Json));
        Assert.Empty(JsonExample.Apply(mismatch, Json));

        CelError unusedError = JsonExample.BuildError(unused);
        JsonExample.AssertBuildError(
            unusedError,
            CelErrorCode.UnusedImplementation,
            "implementation 'nothing_int' has no matching overload declaration");
        Assert.Null(unusedError.SourceLocation);
        JsonExample.AssertBuildErrorAt(
            JsonExample.BuildError(mismatch),
            CelErrorCode.ImplementationSignatureMismatch,
            "implementation 'twice_int' does not match the signature twice(int) -> int of the overload",
            "environment JSON 'm'",
            Json.IndexOf("{\"id\": \"twice_int\"", StringComparison.Ordinal));
    }

    // An overload whose type no source declares is TypeNotDeclared at Build, and like the other errors of this kind
    // it carries the place of the overload in the JSON; the overload is left out of the later checks, so its
    // missing implementation is no second error.
    [Fact]
    public void OverloadOfATypeThatNoSourceDeclares_IsTypeNotDeclaredAtTheOverload()
    {
        const string Json = """{"name": "m", "functions": [{"name": "total", "overloads": [{"id": "total_order", "args": ["acme.sales.Order"], "return": "double"}]}]}""";
        var builder = JsonExample.NewBuilder();
        Assert.Empty(JsonExample.Apply(builder, Json));

        JsonExample.AssertBuildErrorAt(
            JsonExample.BuildError(builder),
            CelErrorCode.TypeNotDeclared,
            "overload 'total_order' has type 'acme.sales.Order', which no type source or extension of the environment declares",
            "environment JSON 'm'",
            Json.IndexOf("{\"id\": \"total_order\"", StringComparison.Ordinal));
    }

    // One identifier for two different overloads: the sources are named, the earlier overload in the text first,
    // and the place is that overload.
    [Fact]
    [Trait("Requirement", "+R-8")]
    public void OneIdentifierForTwoOverloads_IsDuplicateOverloadId()
    {
        const string Json = """
            {"name": "m", "functions": [
              {"name": "f", "overloads": [{"id": "same", "args": ["int"], "return": "int"}]},
              {"name": "g", "overloads": [{"id": "same", "args": ["string"], "return": "int"}]}
            ]}
            """;
        var builder = JsonExample.NewBuilder().AddFunctionImplementation("same", Echo);
        Assert.Empty(JsonExample.Apply(builder, Json));

        CelError error = JsonExample.BuildError(builder);

        Assert.Equal(CelErrorCode.DuplicateOverloadId, error.Code);
        Assert.Equal("overload id 'same' is used by both environment JSON 'm' and environment JSON 'm'", error.Message);
        CelSourceLocation place = Assert.NotNull(error.SourceLocation);
        Assert.Equal(("environment JSON 'm'", Json.IndexOf("{\"id\": \"same\"", StringComparison.Ordinal)), (place.SourceName, place.Offset));
    }

    // Two overloads of one function with the same signature overlap; the one with the smaller identifier is first.
    [Fact]
    [Trait("Requirement", "+R-8")]
    public void TwoOverloadsOfOneSignature_AreOverlappingOverloads()
    {
        const string Json = """
            {"name": "m", "functions": [{"name": "f", "overloads": [
              {"id": "f_a", "args": ["int"], "return": "int"},
              {"id": "f_b", "args": ["int"], "return": "int"}
            ]}]}
            """;
        var builder = JsonExample.NewBuilder()
            .AddFunctionImplementation("f_a", Echo)
            .AddFunctionImplementation("f_b", Echo);
        Assert.Empty(JsonExample.Apply(builder, Json));

        CelError error = JsonExample.BuildError(builder);

        Assert.Equal(CelErrorCode.OverlappingOverloads, error.Code);
        Assert.Equal(
            "overload 'f_a' of environment JSON 'm' overlaps overload 'f_b' of environment JSON 'm' of function 'f'",
            error.Message);
        CelSourceLocation place = Assert.NotNull(error.SourceLocation);
        Assert.Equal(("environment JSON 'm'", Json.IndexOf("{\"id\": \"f_a\"", StringComparison.Ordinal)), (place.SourceName, place.Offset));
    }

    private static long Evaluate(CelEnvironment environment, string expression) =>
        environment.Compile<long>(expression).GetProgramOrThrow()
            .Evaluate(new CelBindings(), cancellationToken: CancellationToken.None)
            .GetValueOrThrow();
}

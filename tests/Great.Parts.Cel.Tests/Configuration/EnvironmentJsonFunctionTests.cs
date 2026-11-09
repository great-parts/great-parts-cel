using System;
using System.Linq;
using System.Threading;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Xunit;
using static Great.Parts.Cel.Tests.Configuration.DescriptionTestSupport;

namespace Great.Parts.Cel.Tests.Configuration;

// The key functions: a function is AddFunction(declaration) with its overloads in the order of the array and without
// implementations, which the application registers in code by the identifier of the overload.
public sealed class EnvironmentJsonFunctionTests
{
    private static readonly CelFunctionImplementation Echo = arguments => arguments[0];

    private static string[] Functions(CelEnvironmentBuilder builder) =>
        [.. builder.FunctionDeclarations.Select(function => function.Name + ": " + string.Join(
            "; ",
            function.Overloads.Select(overload =>
                $"{overload.OverloadId} | {overload.IsReceiverStyle} | {string.Join(", ", overload.ParameterTypes)} | {overload.ResultType}")))];

    [Fact]
    public static void Function_IsTheDeclarationWithItsOverloadsInOrder()
    {
        CelEnvironmentBuilder inCode = NewBuilder().AddFunction(new CelFunctionDecl(
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
        CelEnvironmentBuilder fromJson = NewBuilder();

        Assert.Empty(Apply(fromJson, """
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
            """));

        Assert.Equal(Functions(inCode), Functions(fromJson));
    }

    // The implementation is bound by the identifier, whether it is registered before or after Apply.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public static void Implementation_IsBoundByTheIdentifier(bool beforeApply)
    {
        const string Json = """
            {"functions": [{"name": "twice", "overloads": [
              {"id": "twice_int", "args": ["int"], "return": "int"},
              {"id": "int_twice", "target": "int", "return": "int"}
            ]}]}
            """;
        CelEnvironmentBuilder builder = NewBuilder();
        if (beforeApply)
        {
            Register(builder);
        }

        Assert.Empty(Apply(builder, Json));
        if (!beforeApply)
        {
            Register(builder);
        }

        CelEnvironment environment = builder.Build().GetEnvironmentOrThrow();

        Assert.Equal(42L, Evaluate(environment, "twice(21)"));
        Assert.Equal(42L, Evaluate(environment, "21.twice()"));

        static void Register(CelEnvironmentBuilder target) => target
            .AddFunctionImplementation<long, long>("twice_int", number => number * 2)
            .AddFunctionImplementation<long, long>("int_twice", number => number * 2);
    }

    // The same function twice is one declaration, so its overload is neither a duplicate nor an overlap.
    [Fact]
    public static void FullRepeatOfAnOverload_IsOneDeclaration()
    {
        const string Json = """
            {"functions": [
              {"name": "twice", "overloads": [{"id": "twice_int", "args": ["int"], "return": "int"}]},
              {"name": "twice", "overloads": [{"id": "twice_int", "args": ["int"], "return": "int"}]}
            ]}
            """;
        CelEnvironmentBuilder builder = NewBuilder().AddFunctionImplementation<long, long>("twice_int", number => number * 2);
        Assert.Empty(Apply(builder, Json));

        Assert.Equal(42L, Evaluate(builder.Build().GetEnvironmentOrThrow(), "twice(21)"));
    }

    [Theory]
    [InlineData("""{"functions": [^{"overloads": [{"id": "a", "return": "int"}]}]}""", "has no 'name'")]
    [InlineData("""{"functions": [^{"name": "f"}]}""", "has no 'overloads'")]
    [InlineData("""{"functions": [^{"name": "f", "overloads": []}]}""", "has no overloads")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [^{"return": "int"}]}]}""", "has no 'id'")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [^{"id": "a"}]}]}""", "has no 'return'")]
    public static void MissingKey_IsAnErrorAtTheObject(string marked, string detail) =>
        AssertError(marked, detail);

    [Theory]
    [InlineData("""{"functions": [{"name": ^"", "overloads": [{"id": "a", "return": "int"}]}]}""", "name of a function is empty")]
    [InlineData("""{"functions": [{"name": ^1, "overloads": [{"id": "a", "return": "int"}]}]}""", "'name' must be a string")]
    [InlineData("""{"functions": [{"name": "f", "description": ^1, "overloads": [{"id": "a", "return": "int"}]}]}""", "'description' must be a string")]
    [InlineData("""{"functions": [{"name": "f", "overloads": ^{}}]}""", "'overloads' must be an array")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [^1]}]}""", "must be an object")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [{"id": ^"", "return": "int"}]}]}""", "id of an overload is empty")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [{"id": ^1, "return": "int"}]}]}""", "'id' must be a string")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [{"id": "a", "return": ^1}]}]}""", "'return' must be a string")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [{"id": "a", "return": ^"list<>"}]}]}""", "not a valid type record")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [{"id": "a", "return": "int", "target": ^"~"}]}]}""", "not a valid type record")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [{"id": "a", "return": "int", "target": ^["int"]}]}]}""", "'target' must be a string")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [{"id": "a", "return": "int", "args": ^"int"}]}]}""", "'args' must be an array")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [{"id": "a", "return": "int", "args": ["int", ^1]}]}]}""", "must be a string")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [{"id": "a", "return": "int", "args": ["int", ^"list<int, int>"]}]}]}""", "takes 1 type parameter")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [{"id": "a", "return": "int", "examples": ^"f()"}]}]}""", "'examples' must be an array")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [{"id": "a", "return": "int", "examples": [^1]}]}]}""", "must be a string")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [{"id": "a", "return": ^"timestamp"}]}]}""", "'timestamp' is not supported")]
    public static void EmptyNameOrValueOfTheWrongKind_IsAnErrorAtTheValue(string marked, string detail) =>
        AssertError(marked, detail);

    [Theory]
    [InlineData("""{"functions": [{"name": "f", ^"doc": "d", "overloads": [{"id": "a", "return": "int"}]}]}""", "unknown key 'doc'")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [{"id": "a", "return": "int", ^"cost": 1}]}]}""", "unknown key 'cost'")]
    [InlineData("""{"functions": [{"name": "f", "overloads": [{"id": "a", "return": "int", ^"id": "b"}]}]}""", "'id' is repeated")]
    [InlineData("""{"functions": [{"name": "f", ^"name": "g", "overloads": [{"id": "a", "return": "int"}]}]}""", "'name' is repeated")]
    public static void UnknownOrRepeatedKey_IsAnErrorAtTheKey(string marked, string detail) =>
        AssertError(marked, detail);

    // Every element is read and every error is reported, in the order of the text.
    [Fact]
    public static void EveryFunctionAndOverload_IsChecked() =>
        AssertErrors(
            """{"functions": [^{"name": "f"}, {"name": "g", "overloads": [{"id": ^"", "return": ^1}, ^{"id": "b"}]}]}""",
            Label,
            "has no 'overloads'",
            "id of an overload is empty",
            "'return' must be a string",
            "has no 'return'");

    // A type parameter belongs to an overload: it is right in the target, the arguments and the result.
    [Fact]
    public static void TypeParameter_IsRightInAnOverload()
    {
        CelEnvironmentBuilder builder = NewBuilder();

        Assert.Empty(Apply(builder, """{"functions": [{"name": "f", "overloads": [{"id": "f_list", "target": "list<~A>", "args": ["~A"], "return": "~A"}]}]}"""));
    }

    [Fact]
    public static void NameOfAnOperator_IsInvalidFunctionNameAtBuild()
    {
        const string Json = """{"name": "m", "functions": [{"name": "_+_", "overloads": [{"id": "plus_int", "args": ["int", "int"], "return": "int"}]}]}""";
        CelEnvironmentBuilder builder = NewBuilder();
        Assert.Empty(Apply(builder, Json));

        AssertBuildError(
            BuildError(builder),
            CelErrorCode.InvalidFunctionName,
            "function name '_+_' of environment JSON 'm' is not an identifier or a qualified name; operators and internal functions cannot be declared",
            "environment JSON 'm'",
            IndexOf(Json, "{\"name\": \"_+_\""));
    }

    // The text names no source, yet the error has the place of the overload in the JSON.
    [Fact]
    public static void OverloadWithoutAnImplementation_IsMissingImplementationAtTheOverload()
    {
        const string Json = """{"name": "m", "functions": [{"name": "twice", "overloads": [{"id": "twice_int", "args": ["int"], "return": "int"}]}]}""";
        CelEnvironmentBuilder builder = NewBuilder();
        Assert.Empty(Apply(builder, Json));

        AssertBuildError(
            BuildError(builder),
            CelErrorCode.MissingImplementation,
            "overload 'twice_int' of function 'twice' has no implementation",
            "environment JSON 'm'",
            IndexOf(Json, "{\"id\": \"twice_int\""));
    }

    // An implementation of no declared identifier has no place: no declaration of the JSON is named. One that does not fit
    // the declared signature has the place of the overload.
    [Fact]
    public static void ImplementationThatFitsNothing_IsAnErrorAtBuild()
    {
        const string Json = """{"name": "m", "functions": [{"name": "twice", "overloads": [{"id": "twice_int", "args": ["int"], "return": "int"}]}]}""";
        CelEnvironmentBuilder unused = NewBuilder()
            .AddFunctionImplementation<long, long>("twice_int", number => number * 2)
            .AddFunctionImplementation<long, long>("nothing_int", number => number);
        CelEnvironmentBuilder mismatch = NewBuilder().AddFunctionImplementation<string, bool>("twice_int", text => text.Length > 0);
        Assert.Empty(Apply(unused, Json));
        Assert.Empty(Apply(mismatch, Json));

        AssertBuildError(
            BuildError(unused),
            CelErrorCode.UnusedImplementation,
            "implementation 'nothing_int' has no matching overload declaration",
            "environment JSON 'm'",
            null);
        AssertBuildError(
            BuildError(mismatch),
            CelErrorCode.ImplementationSignatureMismatch,
            "implementation 'twice_int' does not match the signature twice(int) -> int of the overload",
            "environment JSON 'm'",
            IndexOf(Json, "{\"id\": \"twice_int\""));
    }

    // An implementation of the code for an overload of the code: the signature mismatch has no place.
    [Fact]
    public static void SignatureMismatchOfTheCode_HasNoPlace()
    {
        CelEnvironmentBuilder builder = NewBuilder()
            .AddFunction(new CelFunctionDecl("twice", CelOverloadDecl.Global("twice_int", [CelType.Int], CelType.Int)))
            .AddFunctionImplementation<string, bool>("twice_int", text => text.Length > 0);

        AssertBuildError(
            BuildError(builder),
            CelErrorCode.ImplementationSignatureMismatch,
            "implementation 'twice_int' does not match the signature twice(int) -> int of the overload",
            Label,
            null);
    }

    // The overload is left out of the later checks, so its missing implementation is no second error.
    [Fact]
    public static void OverloadOfATypeThatNoSourceDeclares_IsTypeNotDeclaredAtTheOverload()
    {
        const string Json = """{"name": "m", "functions": [{"name": "total", "overloads": [{"id": "total_order", "args": ["acme.sales.Order"], "return": "double"}]}]}""";
        CelEnvironmentBuilder builder = NewBuilder();
        Assert.Empty(Apply(builder, Json));

        AssertBuildError(
            BuildError(builder),
            CelErrorCode.TypeNotDeclared,
            "overload 'total_order' has type 'acme.sales.Order', which no type source or extension of the environment declares",
            "environment JSON 'm'",
            IndexOf(Json, "{\"id\": \"total_order\""));
    }

    // Two different overloads with one identifier: the earlier in the text is named first and gives the place.
    [Fact]
    public static void OneIdentifierForTwoOverloads_IsDuplicateOverloadId()
    {
        const string Json = """{"name": "m", "functions": [{"name": "f", "overloads": [{"id": "same", "args": ["int"], "return": "int"}]}, {"name": "g", "overloads": [{"id": "same", "args": ["string"], "return": "int"}]}]}""";
        CelEnvironmentBuilder builder = NewBuilder().AddFunctionImplementation("same", Echo);
        Assert.Empty(Apply(builder, Json));

        CelError error = BuildError(builder);

        AssertBuildError(
            error,
            CelErrorCode.DuplicateOverloadId,
            "overload id 'same' is used by both environment JSON 'm' and environment JSON 'm'",
            "environment JSON 'm'",
            IndexOf(Json, "{\"id\": \"same\""));
    }

    // The identifier that the delegate form of the code derives is the one of the JSON; the application sorts first.
    [Fact]
    public static void OverloadOfTheDescriptionWithTheIdentifierOfADelegate_IsDuplicateOverloadId()
    {
        const string Json = """{"functions": [{"name": "isInn", "overloads": [{"id": "isInn_string", "args": ["dyn"], "return": "bool"}]}]}""";
        CelEnvironmentBuilder builder = NewBuilder().AddFunction("isInn", static (string value) => value.Length == 10);
        Assert.Empty(Apply(builder, Json));

        AssertBuildError(
            BuildError(builder),
            CelErrorCode.DuplicateOverloadId,
            "overload id 'isInn_string' is used by both application and environment JSON",
            Label,
            IndexOf(Json, "{\"id\": \"isInn_string\""));
    }

    // Two overloads of one function with one signature overlap; the one that sorts first by source, then by identifier,
    // is named first.
    [Fact]
    public static void TwoOverloadsOfOneSignature_AreOverlappingOverloads()
    {
        const string Json = """{"name": "m", "functions": [{"name": "f", "overloads": [{"id": "f_b", "args": ["int"], "return": "int"}, {"id": "f_a", "args": ["int"], "return": "int"}]}]}""";
        CelEnvironmentBuilder builder = NewBuilder()
            .AddFunctionImplementation("f_a", Echo)
            .AddFunctionImplementation("f_b", Echo);
        Assert.Empty(Apply(builder, Json));

        AssertBuildError(
            BuildError(builder),
            CelErrorCode.OverlappingOverloads,
            "overload 'f_a' of environment JSON 'm' overlaps overload 'f_b' of environment JSON 'm' of function 'f'",
            "environment JSON 'm'",
            IndexOf(Json, "{\"id\": \"f_a\""));
    }

    // The application sorts before the description whatever the identifiers are; the place is the overload of the JSON.
    [Fact]
    public static void OverlapOfTheCodeAndTheDescription_NamesTheApplicationFirst()
    {
        const string Json = """{"functions": [{"name": "f", "overloads": [{"id": "a_json", "args": ["int"], "return": "int"}]}]}""";
        CelEnvironmentBuilder builder = NewBuilder()
            .AddFunction(new CelFunctionDecl("f", CelOverloadDecl.Global("z_code", [CelType.Int], CelType.Int)))
            .AddFunctionImplementation("z_code", Echo)
            .AddFunctionImplementation("a_json", Echo);
        Assert.Empty(Apply(builder, Json));

        AssertBuildError(
            BuildError(builder),
            CelErrorCode.OverlappingOverloads,
            "overload 'z_code' of application overlaps overload 'a_json' of environment JSON of function 'f'",
            Label,
            IndexOf(Json, "{\"id\": \"a_json\""));
    }

    // The name and the form of a macro: the function of the description is named, at its place.
    [Fact]
    public static void FunctionWithTheFormOfAMacro_IsStandardLibraryConflictAtTheFunction()
    {
        const string Json = """{"functions": [{"name": "has", "overloads": [{"id": "has_int", "args": ["int"], "return": "bool"}]}]}""";
        CelEnvironmentBuilder builder = NewBuilder();
        Assert.Empty(Apply(builder, Json));

        AssertBuildError(
            BuildError(builder),
            CelErrorCode.StandardLibraryConflict,
            "function 'has' of environment JSON conflicts with standard library v1: it has the form of the macro 'has'",
            Label,
            IndexOf(Json, "{\"name\": \"has\""));
    }

    [Fact]
    public static void OverloadThatOverlapsTheStandardLibrary_IsStandardLibraryConflictAtTheOverload()
    {
        const string Json = """{"functions": [{"name": "size", "overloads": [{"id": "size_string_again", "args": ["string"], "return": "int"}]}]}""";
        CelEnvironmentBuilder builder = NewBuilder().AddFunctionImplementation("size_string_again", Echo);
        Assert.Empty(Apply(builder, Json));

        CelError error = BuildError(builder);

        Assert.Equal(CelErrorCode.StandardLibraryConflict, error.Code);
        Assert.StartsWith("overload 'size_string_again' of environment JSON conflicts with standard library v1: ", error.Message, StringComparison.Ordinal);
        Assert.Equal(IndexOf(Json, "{\"id\": \"size_string_again\""), Assert.NotNull(error.SourceLocation).Offset);
    }

    private static long Evaluate(CelEnvironment environment, string expression) =>
        environment.Compile<long>(expression).GetProgramOrThrow()
            .Evaluate(new CelBindings(), cancellationToken: CancellationToken.None)
            .GetValueOrThrow();
}

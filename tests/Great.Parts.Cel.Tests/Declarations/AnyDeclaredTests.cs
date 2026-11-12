using System.Linq;
using System.Threading;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Tests.Evaluation;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Declarations;

// A declaration that uses the type Any at any depth needs an object type named google.protobuf.Any among the descriptions of the
// type sources; without it the declaration is TypeNotDeclared, whatever the declaration is — a variable, a field of a
// described type, a parameter or a result of a function. A parameter or a result of the type Any takes the .NET types of dyn.
public sealed class AnyDeclaredTests
{
    private const string Undeclared = "which no type source or extension of the environment declares";

    private static readonly CelTypeProvider Known = new ObjectSamples.SampleProvider("known", [MessageSamples.KnownAny()], []);

    [Theory]
    [InlineData("any")]
    [InlineData("list(any)")]
    [InlineData("map(string, any)")]
    [InlineData("type(any)")]
    public static void VariableOfTheTypeAny_WithoutADescription_IsTypeNotDeclared(string text)
    {
        CelEnvironmentBuilder builder = new CelEnvironmentBuilder(CelStandardLibrary.V1).AddVariable("x", Parse(text));

        CelError error = Assert.Single(builder.Build().Errors);

        Assert.Equal(CelErrorCode.TypeNotDeclared, error.Code);
        Assert.Equal("variable 'x' has type 'any', " + Undeclared, error.Message);
    }

    [Fact]
    public static void FieldOfADescribedType_WithTheTypeAny_IsTypeNotDeclared()
    {
        CelObjectType holder = CelObjectTypeBuilder.ForDictionary("acme.test.Holder").AddField("payload", CelType.Any).Build();
        CelEnvironmentBuilder builder = new CelEnvironmentBuilder(CelStandardLibrary.V1).AddTypes(CelTypeProvider.Create("holders", holder));

        CelError error = Assert.Single(builder.Build().Errors);

        Assert.Equal(CelErrorCode.TypeNotDeclared, error.Code);
        Assert.Equal("field 'acme.test.Holder.payload' has type 'any', " + Undeclared, error.Message);
    }

    [Fact]
    public static void ParameterAndResultOfAFunction_WithTheTypeAny_AreTypeNotDeclared()
    {
        CelEnvironmentBuilder builder = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddFunction(new CelFunctionDecl("take", CelOverloadDecl.Global("take_any", [CelType.Any], CelType.Int)))
            .AddFunction(new CelFunctionDecl("give", CelOverloadDecl.Global("give_int", [CelType.Int], CelType.Any)));

        CelError[] errors = [.. builder.Build().Errors];

        Assert.Equal([CelErrorCode.TypeNotDeclared, CelErrorCode.TypeNotDeclared], errors.Select(error => error.Code).ToArray());
        Assert.Contains(errors, error => error.Message == "overload 'take_any' has type 'any', " + Undeclared);
        Assert.Contains(errors, error => error.Message == "overload 'give_int' has type 'any', " + Undeclared);
    }

    // A description of another name is no description of the message: the name decides.
    [Fact]
    public static void DescriptionOfAnotherName_DoesNotDeclareAny()
    {
        CelObjectType other = CelObjectTypeBuilder.ForDictionary("acme.test.Any").AddField("url", CelType.String).Build();
        CelEnvironmentBuilder builder = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddTypes(CelTypeProvider.Create("others", other))
            .AddVariable("x", CelType.Any);

        Assert.Equal(CelErrorCode.TypeNotDeclared, Assert.Single(builder.Build().Errors).Code);
    }

    [Fact]
    public static void WithTheDescriptionOfTheMessage_EveryDeclarationOfTheTypeAnyBuilds()
    {
        CelObjectType holder = CelObjectTypeBuilder.ForDictionary("acme.test.Holder").AddField("payload", CelType.Any).Build();
        CelBuildResult result = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddTypes(Known)
            .AddTypes(CelTypeProvider.Create("holders", holder))
            .AddVariable("x", CelType.ListOf(CelType.Any))
            .AddFunction(new CelFunctionDecl("take", CelOverloadDecl.Global("take_any", [CelType.Any], CelType.Int)))
            .AddFunction(new CelFunctionDecl("give", CelOverloadDecl.Global("give_int", [CelType.Int], CelType.Any)))
            .AddFunctionImplementation("take_any", (CelValue value) => value.Kind == CelValueKind.Null ? 0L : 1L)
            .AddFunctionImplementation("give_int", (long number) => CelValue.FromInt64(number))
            .Build();

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Message)));
    }

    // A parameter of the type Any is a CelValue or an object in the implementation, as a parameter of the type dyn is.
    [Theory]
    [InlineData(typeof(CelValue))]
    [InlineData(typeof(object))]
    public static void ParameterOfTheTypeAny_TakesTheValueAsItIs(System.Type parameter)
    {
        CelEnvironmentBuilder builder = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddTypes(Known)
            .AddVariable("anything", CelType.Any)
            .AddFunction(new CelFunctionDecl("take", CelOverloadDecl.Global("take_any", [CelType.Any], CelType.Int)));
        builder = parameter == typeof(CelValue)
            ? builder.AddFunctionImplementation("take_any", (CelValue value) => value.TryGetInt64(out long number) ? number : -1)
            : builder.AddFunctionImplementation("take_any", (object? value) => value is long number ? number : -1L);

        CelEnvironment environment = builder.Build().GetEnvironmentOrThrow();
        CelEvalResult<long> result = environment.Compile<long>("take(anything)").GetProgramOrThrow()
            .Evaluate(new CelBindings().Add("anything", 41L), cancellationToken: CancellationToken.None);

        Assert.Equal(41L, result.GetValueOrThrow());
    }

    // A result of the type Any is checked as dyn is: any value passes, and the value is typed by what it is.
    [Fact]
    public static void ResultOfTheTypeAny_IsAnyValueTypedByItself()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddTypes(Known)
            .AddFunction(new CelFunctionDecl("give", CelOverloadDecl.Global("give_int", [CelType.Int], CelType.Any)))
            .AddFunctionImplementation("give_int", (long number) => number > 0 ? CelValue.FromInt64(number) : CelValue.FromString("none"))
            .Build()
            .GetEnvironmentOrThrow();

        CelProgram<bool> program = environment.Compile<bool>("give(3) == 3 && give(0) == 'none' && type(give(3)) == int").GetProgramOrThrow();

        Assert.True(program.Evaluate(new CelBindings(), cancellationToken: CancellationToken.None).GetValueOrThrow());
    }

    private static CelType Parse(string text) => text switch
    {
        "any" => CelType.Any,
        "list(any)" => CelType.ListOf(CelType.Any),
        "map(string, any)" => CelType.MapOf(CelType.String, CelType.Any),
        _ => CelType.TypeOf(CelType.Any),
    };
}

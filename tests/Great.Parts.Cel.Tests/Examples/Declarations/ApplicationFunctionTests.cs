using System;
using System.Threading;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Examples.Declarations;

// Functions and variables of the application: the overload identifier that AddFunction derives from a
// delegate, receiver overloads, overloads that extend a function of the standard library, type parameters
// in declarations, and an order of the builder calls that changes nothing.
public sealed class ApplicationFunctionTests
{
    private static readonly CelFunctionImplementation Echo = arguments => arguments[0];

    private readonly CelStandardLibrary library = CelStandardLibrary.V1;

    // The identifier is the name and the CEL types of the parameters joined by "_"; without parameters, the
    // name. A second implementation of it shows it.
    [Theory]
    [Trait("Requirement", "F-3")]
    [InlineData("isInn_string", 1)]
    [InlineData("check_int_string", 2)]
    [InlineData("answer", 0)]
    public void AddFunctionWithADelegate_DerivesTheOverloadIdentifier(string overloadId, int parameterCount)
    {
        var builder = new CelEnvironmentBuilder(library);
        _ = parameterCount switch
        {
            0 => builder.AddFunction<long>("answer", () => 42),
            1 => builder.AddFunction<string, bool>("isInn", text => text.Length == 10),
            _ => builder.AddFunction<long, string, bool>("check", (number, text) => text.Length == number),
        };

        CelError error = Assert.Single(builder.AddFunctionImplementation(overloadId, Echo).Build().Errors);

        Assert.Equal((CelErrorCode.DuplicateImplementation, "overload id '" + overloadId + "' has more than one implementation"), (error.Code, error.Message));
    }

    [Fact]
    [Trait("Requirement", "F-3")]
    [Trait("Requirement", "+R-6")]
    public void FunctionsOfTheApplication_AreCalledGlobalOrOnAReceiver()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(library)
            .AddFunction<string, bool>("isInn", text => text.Length == 10)
            .AddFunction(new CelFunctionDecl("isKpp", CelOverloadDecl.Receiver("string_isKpp", CelType.String, [], CelType.Bool)))
            .AddFunctionImplementation<string, bool>("string_isKpp", text => text.Length == 9)
            .AddFunction<long>("answer", () => 42)
            .Build()
            .GetEnvironmentOrThrow();

        Assert.True(Evaluate<bool>(environment, "isInn('0123456789') && '012345678'.isKpp() && !'0'.isKpp()"));
        Assert.Equal(42L, Evaluate<long>(environment, "answer()"));
    }

    // An overload that does not overlap the standard library extends its function: contains on a list next to
    // contains on a string.
    [Fact]
    [Trait("Requirement", "F-3")]
    [Trait("Requirement", "+R-8")]
    public void OverloadThatDoesNotOverlapTheStandardLibrary_ExtendsItsFunction()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(library)
            .AddFunction(new CelFunctionDecl(
                "contains",
                CelOverloadDecl.Receiver("list_contains_int", CelType.ListOf(CelType.Int), [CelType.Int], CelType.Bool)))
            .AddFunctionImplementation("list_contains_int", (CelFunctionImplementation)ListContains)
            .Build()
            .GetEnvironmentOrThrow();

        Assert.True(Evaluate<bool>(environment, "[1, 2].contains(2) && !([1].contains(3)) && 'abc'.contains('b')"));
    }

    // Only overloads bind type parameters: in the type of a variable a parameter is dyn, and a parameter that
    // occurs only in the result type of an overload stays free and checks as dyn.
    [Fact]
    [Trait("Requirement", "F-3")]
    [Trait("Requirement", "+R-10")]
    public void TypeParameterOutsideTheParametersOfAnOverload_IsDyn()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(library)
            .AddVariable("v", CelType.TypeParameter("T"))
            .AddFunction(new CelFunctionDecl("make", CelOverloadDecl.Global("make_int", [CelType.Int], CelType.TypeParameter("R"))))
            .AddFunctionImplementation("make_int", Echo)
            .Build()
            .GetEnvironmentOrThrow();

        Assert.Equal(CelType.Dyn, environment.Compile<CelValue>("v").GetProgramOrThrow().ResultType);
        Assert.Equal(CelType.Dyn, environment.Compile<CelValue>("make(1)").GetProgramOrThrow().ResultType);
        Assert.True(environment.Compile<CelValue>("v + 1 == make(1)").IsSuccess);
    }

    // The overloads of the application are kept by identifier, so two orders of the calls give one environment.
    [Fact]
    [Trait("Requirement", "+R-8")]
    public void OrderOfTheBuilderCalls_ChangesNoResult()
    {
        CelOverloadDecl ofInt = CelOverloadDecl.Global("describe_int", [CelType.Int], CelType.String);
        CelOverloadDecl ofString = CelOverloadDecl.Global("describe_string", [CelType.String], CelType.String);
        CelFunctionImplementation describeInt = _ => CelValue.FromString("int");
        CelFunctionImplementation describeString = _ => CelValue.FromString("string");

        CelEnvironment forward = new CelEnvironmentBuilder(library)
            .AddFunction(new CelFunctionDecl("describe", ofInt, ofString))
            .AddFunctionImplementation("describe_int", describeInt)
            .AddFunctionImplementation("describe_string", describeString)
            .AddVariable("d", CelType.Dyn)
            .Build()
            .GetEnvironmentOrThrow();
        CelEnvironment reverse = new CelEnvironmentBuilder(library)
            .AddVariable("d", CelType.Dyn)
            .AddFunctionImplementation("describe_string", describeString)
            .AddFunctionImplementation("describe_int", describeInt)
            .AddFunction(new CelFunctionDecl("describe", ofString, ofInt))
            .Build()
            .GetEnvironmentOrThrow();

        foreach (CelEnvironment environment in new[] { forward, reverse })
        {
            Assert.Equal("int string", Evaluate<string>(environment, "describe(1) + ' ' + describe('a')"));
            Assert.Equal("string", Evaluate<string>(environment, "describe(d)", new CelBindings().Add("d", "a")));
            Assert.Equal(CelType.String, environment.Compile<CelValue>("describe(d)").GetProgramOrThrow().ResultType);
        }
    }

    private static CelValue ListContains(ReadOnlySpan<CelValue> arguments)
    {
        Assert.True(arguments[0].TryGetList(out CelList? list));
        foreach (CelValue element in list)
        {
            if (element.Equals(arguments[1]))
            {
                return CelValue.FromBoolean(true);
            }
        }

        return CelValue.FromBoolean(false);
    }

    private static T Evaluate<T>(CelEnvironment environment, string expression, CelBindings? bindings = null) =>
        environment.Compile<T>(expression).GetProgramOrThrow().Evaluate(bindings ?? new CelBindings(), cancellationToken: CancellationToken.None).GetValueOrThrow();
}

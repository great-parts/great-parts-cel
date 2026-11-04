using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Declarations;

// What a built environment holds: the overloads that the delegate forms derive, the declaration index with
// receiver overloads, type identifiers and the container, and the implementations made of typed delegates.
public sealed class EnvironmentDeclarationsTests
{
    private static readonly LineTable Lines = new("pad('a', 1)");

    [Fact]
    public void DelegateForms_DeriveTheOverloadFromTheDelegate()
    {
        DeclarationIndex index = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddFunction<ulong>("seed", () => 7)
            .AddFunction<long?, bool>("given", value => value is not null)
            .AddFunction<CelList, CelMap, CelValue>("join", (list, _) => CelValue.FromList(list))
            .AddFunction<object, CelValue, string, string>("tag", (_, _, id) => id)
            .AddFunction<string, byte[], double, ReadOnlyMemory<byte>, bool>("check", (_, _, _, _) => true)
            .Build()
            .GetEnvironmentOrThrow()
            .Declarations;

        Assert.Equal("seed() -> uint seed", Signature(index, "seed", 0));
        Assert.Equal("given(wrapper(int)) -> bool given_int_wrapper", Signature(index, "given", 1));
        Assert.Equal("join(list(dyn), map(dyn, dyn)) -> dyn join_list<dyn>_map<dyn,dyn>", Signature(index, "join", 2));
        Assert.Equal("tag(dyn, dyn, string) -> string tag_dyn_dyn_string", Signature(index, "tag", 3));
        Assert.Equal("check(string, bytes, double, bytes) -> bool check_string_bytes_double_bytes", Signature(index, "check", 4));
    }

    // The overloads of a call: the standard library in the order of its table, then the application by
    // identifier, whatever the order of the calls; global and receiver overloads apart.
    [Fact]
    public void Index_OrdersTheCandidatesAndSeparatesTheCallStyles()
    {
        CelOverloadDecl global = CelOverloadDecl.Global("add_strings", [CelType.String, CelType.String], CelType.String);
        CelOverloadDecl lists = CelOverloadDecl.Global("add_bools", [CelType.Bool, CelType.Bool], CelType.String);
        CelOverloadDecl receiver = CelOverloadDecl.Receiver("string_add", CelType.String, [CelType.String], CelType.String);
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddFunction(new CelFunctionDecl("plus", receiver, global))
            .AddFunction(new CelFunctionDecl("plus", lists))
            .AddFunctionImplementation("string_add", arguments => arguments[0])
            .AddFunctionImplementation("add_strings", arguments => arguments[0])
            .AddFunctionImplementation("add_bools", arguments => arguments[0])
            .Build()
            .GetEnvironmentOrThrow();

        DeclarationIndex index = environment.Declarations;

        Assert.Equal(["add_bools", "add_strings"], index.GetOverloads("plus", false, 2).Select(overload => overload.Declaration.OverloadId));
        Assert.Equal(["add_bools", "add_strings"], index.GetOverloads("plus", 2).Select(overload => overload.Declaration.OverloadId));
        Assert.Equal(["string_add"], index.GetOverloads("plus", true, 2).Select(overload => overload.Declaration.OverloadId));
        Assert.Empty(index.GetOverloads("plus", true, 1));
        Assert.True(index.IsFunction("plus"));
        Assert.Equal(["add_bytes", "add_double", "add_int64", "add_list", "add_string", "add_uint64"], index.GetOverloads("_+_", 2).Select(overload => overload.Declaration.OverloadId));

        OverloadBinding binding = index.GetOverloads("plus", true, 2)[0];
        Assert.Equal(("plus", true, 1UL, "application", false), (binding.Function, binding.IsApplication, binding.FixedCost, binding.Source, binding.DynOnly));
        Assert.Same(receiver, binding.Declaration);
        Assert.NotNull(binding.Implementation);
    }

    [Fact]
    public void Index_HoldsTheCostFunctionOfTheDeclaration()
    {
        CelCostFunction cost = arguments => (ulong)arguments.Length;
        DeclarationIndex index = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddFunction(new CelFunctionDecl("weigh", CelOverloadDecl.Global("weigh_list", [CelType.ListOf(CelType.Dyn)], CelType.Int).WithCost(cost)))
            .AddFunctionImplementation("weigh_list", _ => CelValue.FromInt64(0))
            .Build()
            .GetEnvironmentOrThrow()
            .Declarations;

        Assert.Same(cost, Assert.Single(index.GetOverloads("weigh", 1)).ArgumentCost);
    }

    [Fact]
    public void Index_HoldsTheTypeIdentifiersAndTheContainer()
    {
        DeclarationIndex plain = new CelEnvironmentBuilder(CelStandardLibrary.V1).Build().GetEnvironmentOrThrow().Declarations;
        DeclarationIndex contained = new CelEnvironmentBuilder(CelStandardLibrary.V1).UseContainer("acme.sales").Build().GetEnvironmentOrThrow().Declarations;

        Assert.True(plain.TryGetTypeIdentifier("map", out CelType? map));
        Assert.Equal(CelType.MapOf(CelType.Dyn, CelType.Dyn), map);
        Assert.True(plain.TryGetTypeIdentifier("type", out CelType? type));
        Assert.Equal(CelType.TypeOf(CelType.Dyn), type);
        Assert.True(plain.TryGetTypeIdentifier("null_type", out CelType? nullType));
        Assert.Equal(CelType.Null, nullType);
        Assert.False(plain.TryGetTypeIdentifier("dyn", out _));
        Assert.False(plain.TryGetTypeIdentifier("google.protobuf.Timestamp", out _));
        Assert.Equal(string.Empty, plain.Container);
        Assert.Equal("acme.sales", contained.Container);
    }

    // Only overloads bind type parameters: in the type of a variable they are dyn, while the environment keeps
    // the declaration as given.
    [Fact]
    public void TypeParameterOfAVariable_IsErasedInTheIndex()
    {
        CelType declared = CelType.MapOf(CelType.String, CelType.ListOf(CelType.TypeParameter("V")));
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1).AddVariable("rows", declared).Build().GetEnvironmentOrThrow();

        Assert.True(environment.Declarations.TryGetVariable("rows", out CelVariableDecl? variable));
        Assert.Equal(CelType.MapOf(CelType.String, CelType.ListOf(CelType.Dyn)), variable.Type);
        Assert.Same(declared, Assert.Single(environment.VariableDeclarations).Type);
    }

    [Fact]
    public void GlobalApplicationFunction_IsCalledWithConvertedArguments()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddFunction<long, string, string>("label", (number, text) => text + "#" + number.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .AddFunction<long>("seven", () => 7)
            .Build()
            .GetEnvironmentOrThrow();

        CelEvalResult<string> label = environment.Compile<string>("label(seven() + 1, 'n')").GetProgramOrThrow()
            .Evaluate(new CelBindings(), cancellationToken: CancellationToken.None);

        Assert.Equal("n#8", label.GetValueOrThrow());
    }

    // An int out of the range of an int parameter does not convert: ArgumentConversion at the call, with the
    // position of the parameter from 1.
    [Fact]
    public void ArgumentThatDoesNotConvert_IsArgumentConversion()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddFunction<string, int, string>("pad", (text, width) => text.PadLeft(width))
            .Build()
            .GetEnvironmentOrThrow();

        CelEvalResult<string> result = environment.Compile<string>("pad('a', 4294967296)").GetProgramOrThrow()
            .Evaluate(new CelBindings(), cancellationToken: CancellationToken.None);

        CelError error = Assert.Single(result.Errors);
        Assert.Equal(
            (CelErrorCode.ArgumentConversion, CelErrorStage.Evaluate, "argument '2' of overload 'pad_string_int' of function 'pad' cannot be converted from 'int' to .NET type 'System.Int32'"),
            (error.Code, error.Stage, error.Message));
    }

    // A typed implementation of a receiver overload gets the receiver first; its result enters by the input rows.
    [Fact]
    public void TypedImplementation_ConvertsTheArgumentsAndTheResult()
    {
        OverloadBinding binding = Implemented(
            builder => builder.AddFunctionImplementation<string, long, long?>("string_pad_int", (text, width) => width > text.Length ? width : null));

        Assert.Equal(CelValue.FromInt64(3), Call(binding, CelValue.FromString("ab"), CelValue.FromInt64(3)));
        Assert.Equal(CelValue.Null, Call(binding, CelValue.FromString("abcd"), CelValue.FromInt64(3)));
        Assert.True(Call(binding, CelValue.FromString("ab"), CelValue.FromUInt64(3)).TryGetError(out CelError? error));
        Assert.Equal(
            (CelErrorCode.ArgumentConversion, "argument '2' of overload 'string_pad_int' of function 'pad' cannot be converted from 'uint' to .NET type 'System.Int64'"),
            (error.Code, error.Message));
    }

    [Fact]
    public void UntypedImplementation_GetsTheValuesAsTheyAre()
    {
        CelValue seen = default;
        OverloadBinding binding = Implemented(builder => builder.AddFunctionImplementation("string_pad_int", arguments =>
        {
            seen = arguments[1];
            return arguments[0];
        }));

        Assert.Equal(CelValue.FromString("ab"), Call(binding, CelValue.FromString("ab"), CelValue.FromInt64(3)));
        Assert.Equal(CelValue.FromInt64(3), seen);
    }

    // The receiver overload string.pad(int) -> dyn with the implementation that the call registers.
    private static OverloadBinding Implemented(Func<CelEnvironmentBuilder, CelEnvironmentBuilder> implement)
    {
        CelEnvironmentBuilder builder = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddFunction(new CelFunctionDecl("pad", CelOverloadDecl.Receiver("string_pad_int", CelType.String, [CelType.Int], CelType.WrapperOf(CelType.Int))));
        return Assert.Single(implement(builder).Build().GetEnvironmentOrThrow().Declarations.GetOverloads("pad", true, 2));
    }

    private static CelValue Call(OverloadBinding binding, params CelValue[] arguments) =>
        binding.Implementation!(arguments, new CallSite(binding.Function, 0, Lines));

    // The signature of the single global overload of the function and its identifier.
    private static string Signature(DeclarationIndex index, string function, int argumentCount)
    {
        ImmutableArray<OverloadBinding> overloads = index.GetOverloads(function, argumentCount);
        CelOverloadDecl overload = Assert.Single(overloads).Declaration;
        string text = function + "(" + string.Join(", ", overload.ParameterTypes) + ") -> " + overload.ResultType;
        return text + " " + overload.OverloadId;
    }
}

using System;
using System.Collections.Generic;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Facade;

// The CEL type of a parameter of a typed delegate is the expected type of its .NET type, as for Compile<T>, and must be
// the declared parameter type after erasure, dyn and type parameters taking CelValue and object, and a declared wrapper(P)
// also string or byte[] whose type is P; otherwise the build reports ImplementationSignatureMismatch
// (docs/design/declarations.md, 4011).
public sealed class DelegateParameterTypeTests
{
    private enum Shade
    {
        Light,
    }

    [Fact]
    public void ParameterWithoutTheDeclaredExpectedType_IsImplementationSignatureMismatch()
    {
        string[] mismatches =
        [
            Build(CelType.String, b => b.AddFunctionImplementation("f_p", (Guid _) => true)),
            Build(CelType.Int, b => b.AddFunctionImplementation("f_p", (short _) => true)),
            Build(CelType.Double, b => b.AddFunctionImplementation("f_p", (float _) => true)),
            Build(CelType.Double, b => b.AddFunctionImplementation("f_p", (Half _) => true)),
            Build(CelType.Int, b => b.AddFunctionImplementation("f_p", (Shade _) => true)),
            Build(CelType.WrapperOf(CelType.Int), b => b.AddFunctionImplementation("f_p", (short? _) => true)),
            Build(CelType.ListOf(CelType.Int), b => b.AddFunctionImplementation("f_p", (IReadOnlyList<short> _) => true)),
            Build(CelType.ListOf(CelType.Int), b => b.AddFunctionImplementation("f_p", (long[] _) => true)),
            Build(CelType.ListOf(CelType.Int), b => b.AddFunctionImplementation("f_p", (CelList _) => true)),
            Build(CelType.ListOf(CelType.Int), b => b.AddFunctionImplementation("f_p", (IReadOnlyList<object> _) => true)),
            Build(CelType.Dyn, b => b.AddFunctionImplementation("f_p", (long _) => true)),
            Build(CelType.ListOf(CelType.ListOf(CelType.Int)), b => b.AddFunctionImplementation("f_p", (IReadOnlyList<IReadOnlyList<long>> _) => true)),
            Build(CelType.WrapperOf(CelType.Bytes), b => b.AddFunctionImplementation("f_p", (CelValue _) => true)),
            Build(CelType.WrapperOf(CelType.Bytes), b => b.AddFunctionImplementation("f_p", (object _) => true)),
            Build(CelType.WrapperOf(CelType.Bytes), b => b.AddFunctionImplementation("f_p", (ReadOnlyMemory<byte> _) => true)),
            Build(CelType.WrapperOf(CelType.Int), b => b.AddFunctionImplementation("f_p", (long _) => true)),
            Build(CelType.WrapperOf(CelType.String), b => b.AddFunctionImplementation("f_p", (Guid _) => true)),
        ];

        Assert.All(mismatches, error => Assert.Equal("ImplementationSignatureMismatch", error));
    }

    [Fact]
    public void ParameterWithTheDeclaredExpectedType_Builds()
    {
        string[] built =
        [
            Build(CelType.Int, b => b.AddFunctionImplementation("f_p", (long _) => true)),
            Build(CelType.Int, b => b.AddFunctionImplementation("f_p", (int _) => true)),
            Build(CelType.WrapperOf(CelType.Int), b => b.AddFunctionImplementation("f_p", (long? _) => true)),
            Build(CelType.Dyn, b => b.AddFunctionImplementation("f_p", (CelValue _) => true)),
            Build(CelType.TypeParameter("A"), b => b.AddFunctionImplementation("f_p", (object _) => true)),
            Build(CelType.ListOf(CelType.Dyn), b => b.AddFunctionImplementation("f_p", (CelList _) => true)),
            Build(CelType.ListOf(CelType.TypeParameter("A")), b => b.AddFunctionImplementation("f_p", (IReadOnlyList<object> _) => true)),
            Build(CelType.ListOf(CelType.Int), b => b.AddFunctionImplementation("f_p", (IReadOnlyList<long> _) => true)),
            Build(CelType.ListOf(CelType.Int), b => b.AddFunctionImplementation("f_p", (IReadOnlyList<int> _) => true)),
            Build(CelType.MapOf(CelType.String, CelType.Int), b => b.AddFunctionImplementation("f_p", (IReadOnlyDictionary<string, long> _) => true)),
            Build(CelType.TypeOf(CelType.Dyn), b => b.AddFunctionImplementation("f_p", (CelType _) => true)),
            Build(CelType.WrapperOf(CelType.String), b => b.AddFunctionImplementation("f_p", (string _) => true)),
            Build(CelType.WrapperOf(CelType.Bytes), b => b.AddFunctionImplementation("f_p", (byte[] _) => true)),
            Build(CelType.WrapperOf(CelType.Bytes), b => b.AddFunctionImplementation("f_p", (ReadOnlyMemory<byte>? _) => true)),
        ];

        Assert.All(built, outcome => Assert.Equal("built", outcome));
    }

    // A string or byte[] parameter of a declared wrapper takes the argument null as a null reference and a value as it is.
    [Fact]
    public void ReferenceParameterOfAWrapper_TakesNullAndAValue()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddFunction(new CelFunctionDecl("f", CelOverloadDecl.Global("f_wrapper", [CelType.WrapperOf(CelType.String)], CelType.Bool)))
            .AddFunctionImplementation("f_wrapper", (string? value) => value is null)
            .AddFunction(new CelFunctionDecl("g", CelOverloadDecl.Global("g_wrapper", [CelType.WrapperOf(CelType.Bytes)], CelType.Int)))
            .AddFunctionImplementation("g_wrapper", (byte[]? value) => value is null ? -1L : value.Length)
            .Build()
            .GetEnvironmentOrThrow();

        Assert.True(Evaluate<bool>(environment, "f(null)"));
        Assert.False(Evaluate<bool>(environment, "f('a')"));
        Assert.Equal(-1L, Evaluate<long>(environment, "g(null)"));
        Assert.Equal(2L, Evaluate<long>(environment, "g(b'ab')"));
    }

    // The result of a declared wrapper: string and byte[] build, and a null result is the CEL null, not an error of the result.
    [Fact]
    public void ReferenceResultOfAWrapper_BuildsAndGivesNull()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddFunction(new CelFunctionDecl("f", CelOverloadDecl.Global("f_none", [], CelType.WrapperOf(CelType.String))))
            .AddFunctionImplementation("f_none", static string? () => null)
            .AddFunction(new CelFunctionDecl("g", CelOverloadDecl.Global("g_none", [], CelType.WrapperOf(CelType.Bytes))))
            .AddFunctionImplementation("g_none", static byte[]? () => null)
            .Build()
            .GetEnvironmentOrThrow();

        Assert.Equal(CelValue.Null, Evaluate<CelValue>(environment, "f()"));
        Assert.Equal(CelValue.Null, Evaluate<CelValue>(environment, "g()"));
        Assert.True(Evaluate<bool>(environment, "f() == null && g() == null"));
    }

    // The result of a declared wrapper(int) does not take long: long does not take null.
    [Fact]
    public void ValueTypeResultOfAWrapper_IsImplementationSignatureMismatch()
    {
        CelBuildResult result = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddFunction(new CelFunctionDecl("f", CelOverloadDecl.Global("f_none", [], CelType.WrapperOf(CelType.Int))))
            .AddFunctionImplementation("f_none", static () => 1L)
            .Build();

        Assert.Equal(CelErrorCode.ImplementationSignatureMismatch, Assert.Single(result.Errors).Code);
    }

    // The delegate form infers the overload by the input rows and checks it by the same rule.
    [Fact]
    public void DelegateForm_ChecksItsInferredOverload()
    {
        CelBuildResult small = new CelEnvironmentBuilder(CelStandardLibrary.V1).AddFunction("small", (short _) => true).Build();
        CelBuildResult id = new CelEnvironmentBuilder(CelStandardLibrary.V1).AddFunction("id", (Guid value) => value.ToString()).Build();

        Assert.Equal(
            "implementation 'small_int' does not match the signature small(int) -> bool of the overload",
            Assert.Single(small.Errors).Message);
        Assert.Equal(CelErrorCode.ImplementationSignatureMismatch, Assert.Single(id.Errors).Code);
        Assert.True(new CelEnvironmentBuilder(CelStandardLibrary.V1).AddFunction("big", (long? _) => true).Build().IsSuccess);
    }

    // Collections of the application come with their part: the input rows have no IReadOnlyList<…> yet.
    [Fact]
    public void DelegateFormWithACollectionParameter_IsTypeNotDeclared()
    {
        CelError error = Assert.Single(new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddFunction("sum", (IReadOnlyList<long> items) => items.Count).Build().Errors);

        Assert.Equal(CelErrorCode.TypeNotDeclared, error.Code);
    }

    // A map parameter whose keys and values cannot fail is a lazy view of the argument.
    [Fact]
    public void MapParameter_ReadsTheArgument()
    {
        CelEnvironment environment = new CelEnvironmentBuilder(CelStandardLibrary.V1)
            .AddFunction(new CelFunctionDecl("get", CelOverloadDecl.Global("get_map", [CelType.MapOf(CelType.String, CelType.Int), CelType.String], CelType.Int)))
            .AddFunctionImplementation("get_map", (IReadOnlyDictionary<string, long> map, string key) => map[key])
            .Build()
            .GetEnvironmentOrThrow();

        Assert.Equal(2L, environment.Evaluate<long>("get({'a': 1, 'b': 2}, 'b')", new CelBindings(), cancellationToken: TestContext.Current.CancellationToken).GetValueOrThrow());
    }

    private static T Evaluate<T>(CelEnvironment environment, string expression) =>
        environment.Evaluate<T>(expression, new CelBindings(), cancellationToken: TestContext.Current.CancellationToken).GetValueOrThrow();

    // "built", or the name of the code of the single build error.
    private static string Build(CelType parameterType, Func<CelEnvironmentBuilder, CelEnvironmentBuilder> implement)
    {
        CelBuildResult result = implement(new CelEnvironmentBuilder(CelStandardLibrary.V1)
                .AddFunction(new CelFunctionDecl("f", CelOverloadDecl.Global("f_p", [parameterType], CelType.Bool))))
            .Build();
        return result.IsSuccess ? "built" : Assert.Single(result.Errors).Code.ToString();
    }
}

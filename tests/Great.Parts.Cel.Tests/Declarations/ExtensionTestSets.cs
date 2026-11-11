using System;
using System.Collections.Immutable;
using System.Linq;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Library;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;
using Xunit;

namespace Great.Parts.Cel.Tests.Declarations;

// Sets of the catalog that the tests make themselves: no set of the library exists in this part, and the mechanism — the
// checks of the sets, the index, the syntax of the environment — is the same for every set.
internal static class ExtensionTestSets
{
    internal static readonly OverloadImplementation Forty2 = static (ReadOnlySpan<CelValue> _, in CallSite _) => CelValue.FromInt64(42);

    internal static readonly OverloadImplementation Count = static (ReadOnlySpan<CelValue> arguments, in CallSite _) =>
        CelValue.FromInt64(arguments.Length);

    // A set of the catalog with the given parts; the source of its overloads is the source of the set.
    internal static CatalogExtension Set(
        string name,
        uint version,
        ImmutableArray<OverloadBinding> overloads = default,
        ImmutableArray<Macro> macros = default,
        ImmutableArray<string> dependencies = default,
        ImmutableArray<AbstractTypeDeclaration> abstractTypes = default,
        ImmutableArray<CelVariableDecl> variables = default) =>
        new(name, version, overloads.IsDefault ? [] : overloads, abstractTypes, variables, dependencies, macros);

    // The set of the catalog with the definition, as the catalog hands it out; the versions are those the catalog would list.
    internal static CelExtension Of(CatalogExtension definition, params uint[] versions) =>
        new(definition.Name, definition.Version, definition, [.. versions]);

    // The set of the catalog that has no such version.
    internal static CelExtension Missing(string name, uint version, params uint[] versions) =>
        new(name, version, null, [.. versions]);

    // A global overload of a set.
    internal static OverloadBinding Global(
        string set,
        string function,
        string id,
        CelType[] parameters,
        CelType result,
        OverloadImplementation? implementation = null,
        bool shallow = false,
        CallValidator? validator = null) =>
        new(function, CelOverloadDecl.Global(id, [.. parameters], result), implementation ?? Forty2, 1, null, set, shallowArguments: shallow, validator: validator);

    // A receiver overload of a set.
    internal static OverloadBinding Receiver(
        string set,
        string function,
        string id,
        CelType receiver,
        CelType[] parameters,
        CelType result,
        OverloadImplementation? implementation = null,
        CallValidator? validator = null) =>
        new(function, CelOverloadDecl.Receiver(id, receiver, [.. parameters], result), implementation ?? Forty2, 1, null, set, validator: validator);

    // The source of a set of the catalog as build errors write it.
    internal static string Source(string name, uint version) => $"extension {name} v{version}";

    // The macro test.succ(e): the target must be the identifier test, and e + 1 is the expansion.
    internal static Macro Successor() =>
        new(
            "succ",
            "test",
            1,
            isReceiver: true,
            static (_, _) => MacroCheck.Expand,
            static (nodes, _, arguments, offset) => nodes.Call("_+_", null, [arguments[0], nodes.Constant(CelValue.FromInt64(1), offset)], offset));

    // The macro test.fail(e), whose call is an error at the argument, and the macro test.any(...) of any number of arguments that
    // expands to the number of its arguments.
    internal static Macro Failing() =>
        new(
            "fail",
            "test",
            1,
            isReceiver: true,
            static (_, arguments) => MacroCheck.Error("it always fails", arguments[0]),
            static (nodes, _, _, offset) => nodes.Constant(CelValue.FromInt64(0), offset));

    internal static Macro Counting() =>
        new(
            "count",
            "test",
            Macro.AnyArgumentCount,
            isReceiver: true,
            static (_, _) => MacroCheck.Expand,
            static (nodes, _, arguments, offset) => nodes.Constant(CelValue.FromInt64(arguments.Length), offset));

    // A function of the application with no implementation of its own, for the checks of names.
    internal static CelFunctionDecl Function(string name, params CelOverloadDecl[] overloads) => new(name, overloads);

    // The only error of a failed build.
    internal static CelError OnlyError(CelEnvironmentBuilder builder)
    {
        CelBuildResult result = builder.Build();
        Assert.False(result.IsSuccess);
        return Assert.Single(result.Errors);
    }

    // The code and the message of every error of a failed build, in order.
    internal static string[] Errors(CelEnvironmentBuilder builder)
    {
        CelBuildResult result = builder.Build();
        Assert.False(result.IsSuccess);
        return [.. result.Errors.Select(error => $"{(int)error.Code} {error.Message}")];
    }

    internal static CelEnvironment BuildEnvironment(CelEnvironmentBuilder builder)
    {
        CelBuildResult result = builder.Build();
        Assert.True(result.IsSuccess, string.Join("; ", result.Errors.Select(error => error.Message)));
        return result.Environment;
    }

    internal static CelEnvironmentBuilder Builder() => new(CelStandardLibrary.V1);
}

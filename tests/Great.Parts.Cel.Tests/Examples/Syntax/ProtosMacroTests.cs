using Great.Parts.Cel.Tests.Examples.Library;
using Great.Parts.Cel.Tests.Examples.Values;
using Xunit;
using Proto2 = global::Cel.Expr.Conformance.Proto2;

namespace Great.Parts.Cel.Tests.Examples.Syntax;

// The macros of the set protos, proto.getExt(message, extension) and proto.hasExt(message, extension) (the page of the
// extension sets, "protos"): the parser expands a call on the receiver proto with two arguments into the selection of the
// field whose name is the full name of the extension, written as a chain of identifiers, and into has() of that selection,
// and only when the set is in the environment. A second argument that is not a chain of identifiers — a constant, a list, a
// call, an operator, an index — is InvalidMacroCall (1006), "invalid call of macro '<getExt or hasExt>': invalid extension
// field", at the argument; the call with one or three arguments is not a macro, and without the set the call stays a call
// and the receiver is undeclared. The texts and the places of the parse errors are those of cel-go v0.32.0 (a probe of the oracle, 2026-11-11). Rows
// are those of ExtensionRows. p2x is a proto2 TestAllTypes; the environments have no container, so the names are written in
// full.
[Trait("Requirement", "F-1")]
[Trait("Requirement", "F-4")]
[Trait("Requirement", "F-3")]
public sealed class ProtosMacroTests
{
    private const string Prefix = "invalid call of macro ";

    private const string Extension = "cel.expr.conformance.proto2.int32_ext";

    // The place is the argument; for an operator the operator, for a call its "(", for an index its "[". A single name is no chain:
    // an extension name has at least one dot.
    private const string Errors = $$"""
        Parse | proto.getExt(p2x, ^1) | InvalidMacroCall | {{Prefix}}'getExt': invalid extension field
        Parse | proto.hasExt(p2x, ^'a') | InvalidMacroCall | {{Prefix}}'hasExt': invalid extension field
        Parse | proto.getExt(p2x, a ^+ b) | InvalidMacroCall | {{Prefix}}'getExt': invalid extension field
        Parse | proto.getExt(p2x, ^a) | InvalidMacroCall | {{Prefix}}'getExt': invalid extension field
        Parse | proto.getExt(p2x, ^true) | InvalidMacroCall | {{Prefix}}'getExt': invalid extension field
        Parse | proto.hasExt(p2x, ^null) | InvalidMacroCall | {{Prefix}}'hasExt': invalid extension field
        Parse | proto.getExt(p2x, f^()) | InvalidMacroCall | {{Prefix}}'getExt': invalid extension field
        Parse | proto.getExt(p2x, a.f^()) | InvalidMacroCall | {{Prefix}}'getExt': invalid extension field
        Parse | proto.getExt(p2x, a^[0]) | InvalidMacroCall | {{Prefix}}'getExt': invalid extension field
        Parse | proto.hasExt(p2x, ^[a]) | InvalidMacroCall | {{Prefix}}'hasExt': invalid extension field
        Parse | proto.getExt(1, ^2) + proto.hasExt(p2x, ^3) | InvalidMacroCall InvalidMacroCall | {{Prefix}}'getExt': invalid extension field ;; {{Prefix}}'hasExt': invalid extension field
        """;

    // A chain of identifiers is an extension name whatever it names, and the selection that the macro makes is at the "(" of
    // the call; whether the extension exists is a question of type checking, the rows of ProtosExtensionTests.
    private const string Expanded = """
        Check | proto.getExt^(p2x, a.b.c) | NoSuchField | undefined field 'a.b.c' in type 'cel.expr.conformance.proto2.TestAllTypes'
        Check | proto.hasExt^(p2x, a.b) | NoSuchField | undefined field 'a.b' in type 'cel.expr.conformance.proto2.TestAllTypes'
        """;

    // Not a macro: one argument or three, a call that is not on proto. Without the set the call of a macro is not
    // expanded either; the receiver `proto` is a name that is not declared, an error at the receiver. The function is not
    // named: a node with an error gives no further error and a call with such a part is not looked up (the page of type
    // checking, "Overload candidates" and "The type error and the list of errors"), the same as in ExtensionMacroTests, so
    // the count of errors is not that of cel-go, which names the function too, and the checked part is the stage, the code and
    // the place of each error. The arguments are checked: in the second row the chain of identifiers of the extension starts
    // with the name `cel`, which is not declared either, an error at its first name.
    private const string NotMacros = $$"""
        Check | ^proto.getExt(p2x) | UndeclaredReference | undeclared reference to 'proto' (in container '')
        Check | ^proto.hasExt(p2x, ^{{Extension}}, 1) | UndeclaredReference UndeclaredReference | undeclared reference to 'proto' (in container '') ;; undeclared reference to 'cel' (in container '')
        Check | ^proto.getExt() | UndeclaredReference | undeclared reference to 'proto' (in container '')
        """;

    private const string WithoutTheSet = """
        Check | ^proto.getExt(p2x, 1) | UndeclaredReference | undeclared reference to 'proto' (in container '')
        Check | ^proto.hasExt(p2x, 'a') | UndeclaredReference | undeclared reference to 'proto' (in container '')
        """;

    private readonly CelEnvironment withSet = Builder().AddExtension(CelExtensions.Protos(0)).Build().GetEnvironmentOrThrow();

    private readonly CelEnvironment withoutSet = Builder().Build().GetEnvironmentOrThrow();

    private readonly CelEnvironment otherSets = Builder()
        .AddExtension(CelExtensions.Strings(5))
        .AddExtension(CelExtensions.Math(3))
        .AddExtension(CelExtensions.Lists(4))
        .Build()
        .GetEnvironmentOrThrow();

    [Fact]
    public void ArgumentThatIsNotAChainOfIdentifiers_IsInvalidMacroCall()
    {
        Assert.Empty(ExtensionRows.CheckErrors(withSet, Errors));
    }

    [Fact]
    public void ChainOfIdentifiers_IsExpanded()
    {
        Assert.Empty(ExtensionRows.CheckErrors(withSet, Expanded));
    }

    [Fact]
    public void CallThatIsNotAMacro_IsNotExpanded()
    {
        Assert.Empty(ExtensionRows.CheckErrors(withSet, NotMacros));
    }

    [Fact]
    public void WithoutTheSet_TheMacroIsNotExpanded()
    {
        Assert.Empty(ExtensionRows.CheckErrors(withoutSet, WithoutTheSet));
    }

    // A macro belongs to its own set: the sets of strings, math and lists do not bring proto.getExt.
    [Fact]
    public void WithOtherSets_TheMacroIsNotExpanded()
    {
        Assert.Empty(ExtensionRows.CheckErrors(otherSets, WithoutTheSet));
    }

    private static CelEnvironmentBuilder Builder() => new CelEnvironmentBuilder(CelStandardLibrary.V1)
        .AddTypes(ProtobufExample.TypesAll)
        .AddVariable<Proto2.TestAllTypes>("p2x");
}

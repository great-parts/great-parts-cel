using System.Collections.Immutable;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Library.Extensions;

// The extension set bindings, version 1: the macro cel.bind and the function cel.@block with the overload cel_block_list,
// cel.@block(list(dyn), T) -> T. Like conditional, cel.@block is a special form without an implementation: the planner
// evaluates it, from a list literal of slots and the result. It costs nothing beyond its slots and its
// result. No text can write its name; a tree built by the library can.
internal static class BindingsExtension
{
    internal const string SetName = "bindings";
    internal const uint Version = 1;
    private const string Source = "extension bindings v1";
    private const string BlockFunction = "cel.@block";

    // The definition of the version, or null for a version the set does not have.
    internal static CatalogExtension? Create(uint version) =>
        version == Version ? new CatalogExtension(SetName, Version, Overloads(), macros: ExtensionMacros.Bindings) : null;

    // cel_block_list: the first argument has to be a list literal without optional elements, which the planner reads the slots
    // from; any other first argument is NoMatchingOverload at the call, with the usual text. The type of the block is the
    // type of its result, the parameter T.
    internal static CallValidation ValidateBlock(CallShape call) =>
        call.Arguments[0] is { Elements: not null, HasOptionalElements: false }
            ? CallValidation.Ok
            : new CallValidation(CelErrorCode.NoMatchingOverload, string.Empty, null);

    private static ImmutableArray<OverloadBinding> Overloads()
    {
        CelType result = CelType.TypeParameter("T");
        return
        [
            new OverloadBinding(
                BlockFunction,
                new CelOverloadDecl("cel_block_list", false, [CelType.ListOf(CelType.Dyn), result], result),
                null,
                0,
                null,
                Source,
                validator: ValidateBlock),
        ];
    }
}

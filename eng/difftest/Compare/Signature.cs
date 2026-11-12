using System;
using System.Collections.Generic;
using System.Linq;
using Cel.Expr;
using Great.Parts.Cel.Eng.Difftest.Profile;
using Great.Parts.Cel.Eng.Difftest.Sides;

namespace Great.Parts.Cel.Eng.Difftest.Compare;

/// <summary>A divergence of a minimized case.</summary>
/// <param name="Signature">The signature, <c>&lt;node&gt;(&lt;types&gt;) &lt;kind&gt;</c>.</param>
/// <param name="Kind">The kind.</param>
/// <param name="Case">The minimized case.</param>
/// <param name="Library">The outcome of the minimized case on the library.</param>
/// <param name="Oracle">The outcome of the minimized case on cel-go.</param>
internal sealed record Divergence(string Signature, DivergenceKind Kind, GeneratedCase Case, SideOutcome Library, SideOutcome Oracle);

/// <summary>
/// The signature of a divergence, <c>&lt;node&gt;(&lt;types&gt;) &lt;kind&gt;</c> of the root of the minimized case
/// (docs/design/eng-difftest.md, "Outcomes and signatures"), in the form of the registry of known differences.
/// </summary>
internal static class Signature
{
    /// <summary>The type of an argument that is an error on both sides.</summary>
    internal const string ErrorType = "error";

    /// <summary>
    /// The signature of a divergent case. The node is <c>parse</c> without types when either side fails to parse; otherwise
    /// it follows the root: the profile overload whose parameter types fit the types of the argument values, or the function
    /// name when none fits; <c>select</c> and <c>has</c> with the type of the operand; <c>comprehension</c> with the type of the
    /// range; <c>list</c> with the element types; <c>map</c> with key and value types entry by entry;
    /// <c>literal</c> with its type; <c>ident</c> with the type of its value. The arguments, elements, keys and values are
    /// evaluated on both sides to get their types.
    /// </summary>
    /// <param name="divergent">The minimized case.</param>
    /// <param name="library">The outcome of the case on the library.</param>
    /// <param name="oracle">The outcome of the case on cel-go.</param>
    /// <param name="kind">The kind of the divergence.</param>
    /// <param name="librarySide">The library, to evaluate the children of the root.</param>
    /// <param name="oracleSide">The oracle, to evaluate the children of the root.</param>
    internal static string Of(
        GeneratedCase divergent,
        SideOutcome library,
        SideOutcome oracle,
        DivergenceKind kind,
        ICaseSide librarySide,
        ICaseSide oracleSide)
    {
        string Type(Expr child) => ChildType(divergent, child, librarySide, oracleSide);
        string node = library.FailedToParse || oracle.FailedToParse
            ? "parse()"
            : divergent.Expression switch
            {
                Call call => Node(call, [.. call.Args.Select(Type)]),
                ListLiteral list => Node("list", list.Elements.Select(Type)),
                MapLiteral map => Node("map", map.Entries.SelectMany(entry => new[] { Type(entry.Key), Type(entry.Value) })),
                Select select => Node(select.Optional ? ProfileTables.OptionalSelectOverload : "select", [Type(select.Operand)]),
                PresenceTest test => Node("has", [Type(test.Operand)]),
                Comprehension comprehension => Node("comprehension", [Type(comprehension.Range)]),
                Ident ident => Node("ident", [IdentType(divergent, ident)]),
                Expr literal => Node("literal", [LiteralType(literal)]),
            };
        return node + " " + kind.Name();
    }

    private static string Node(Call call, IReadOnlyList<string> types)
    {
        // An index over an optional whose held type is a list or a map: the name has no parameters, so an int key would fit both
        // overloads of a pair and the first would win. The overload and the receiver are named by the held type that the generator
        // knows (docs/design/eng-difftest.md, "Signatures"). A held dyn or an unknown type keeps the first overload that fits.
        string? held = call.Container is { Kind: ProfileType.ListType or ProfileType.MapType } container && types.Count == 2
            ? container.Kind.Name()
            : null;
        string prefix = "optional_" + held + "_";
        ProfileOverload? overload = ProfileTables.Overloads.FirstOrDefault(candidate =>
            candidate.Function == call.Function
            && candidate.Receiver == call.Receiver
            && candidate.Parameters.Count == types.Count
            && (held is null || candidate.Id.StartsWith(prefix, StringComparison.Ordinal))
            && candidate.Parameters.Zip(types).All(pair => Fits(pair.First, pair.Second)));
        IReadOnlyList<string> shown = overload is not null && held is not null
            ? [ProfileType.OptionalType.Name() + "(" + held + ")", types[1]]
            : types;
        return Node(overload?.Id ?? call.Function, shown);
    }

    // A parameter fits the type name of an argument value: a type parameter and dyn fit any value, list(A) and map(A, B) fit any
    // list and map, since the names have no parameters; an error fits none.
    private static bool Fits(ProfileType parameter, string type) =>
        type != ErrorType
        && (parameter.IsParameter() || parameter == ProfileType.DynType || string.Equals(parameter.Name(), type, StringComparison.Ordinal));

    // The type of the value of an identifier: of its binding, type for a type identifier, error for an unbound variable.
    private static string IdentType(GeneratedCase divergent, Ident ident) =>
        divergent.Bindings.TryGetValue(ident.Name, out Value? value) ? ValueText.TypeName(value)
        : ProfileTables.TypeIdentifiers.Contains(ident.Name) || ProfileTables.AbstractTypeIdentifiers.Contains(ident.Name)
            || ident.Name == ProfileTables.OptionalTypeIdentifier ? ProfileType.TypeType.Name()
        : ErrorType;

    private static string Node(string node, IEnumerable<string> types) => node + "(" + string.Join(", ", types) + ")";

    // The type of the value of a child of the root with the bindings of the case; "error" when it is an error on both sides.
    // After minimization the child agrees on both sides, so either value gives the type; cel-go goes first.
    private static string ChildType(GeneratedCase divergent, Expr child, ICaseSide librarySide, ICaseSide oracleSide)
    {
        // A child of an abstract type is evaluated as string(child) and keeps the name of its type: the value of the string is no type.
        // An optional child is evaluated as a pair and is named optional_type, and a literal that holds an optional as its size and
        // is named list or map: the value that comes back is no type of theirs either.
        string text = ExprPrinter.PrintCase(child);
        string? abstractType = ProfileTables.AbstractResult(child)?.Name()
            ?? (ProfileTables.IsOptional(child) ? ProfileType.OptionalType.Name() : null)
            ?? (ProfileTables.HoldsOptional(child) ? (child is ListLiteral ? ProfileType.ListType.Name() : ProfileType.MapType.Name()) : null);
        SideOutcome oracle = oracleSide.Evaluate(text, divergent.Bindings);
        if (!oracle.IsError)
        {
            return abstractType ?? ValueText.TypeName(oracle.Result!);
        }

        SideOutcome library = librarySide.Evaluate(text, divergent.Bindings);
        return library.IsError ? ErrorType : abstractType ?? ValueText.TypeName(library.Result!);
    }

    private static string LiteralType(Expr literal) => literal switch
    {
        IntLiteral => ProfileType.IntType.Name(),
        UintLiteral => ProfileType.UintType.Name(),
        DoubleLiteral => ProfileType.DoubleType.Name(),
        BoolLiteral => ProfileType.BoolType.Name(),
        StringLiteral => ProfileType.StringType.Name(),
        BytesLiteral => ProfileType.BytesType.Name(),
        NullLiteral => ProfileType.NullType.Name(),
        _ => throw new ArgumentOutOfRangeException(nameof(literal), literal, "Not a node of the profile."),
    };
}

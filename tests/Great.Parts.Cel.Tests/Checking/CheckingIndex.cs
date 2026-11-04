using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using Great.Parts.Cel.Checking;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;
using Xunit;

namespace Great.Parts.Cel.Tests.Checking;

// A declaration index for the tests of checking, independent of how much of the standard library is
// implemented: the operators that the parser and the macro expansions create, with the signatures of the
// language definition, a few type identifiers and the functions and variables of the application that the
// tests name.
internal static class CheckingIndex
{
    private static readonly CelType A = CelType.TypeParameter("A");
    private static readonly CelType K = CelType.TypeParameter("K");
    private static readonly CelType V = CelType.TypeParameter("V");

    private static readonly ImmutableArray<OverloadBinding> Library =
    [
        Global("_&&_", "logical_and", [CelType.Bool, CelType.Bool], CelType.Bool),
        Global("_||_", "logical_or", [CelType.Bool, CelType.Bool], CelType.Bool),
        Global("!_", "logical_not", [CelType.Bool], CelType.Bool),
        Global("@not_strictly_false", "not_strictly_false", [CelType.Bool], CelType.Bool),
        Global("_?_:_", "conditional", [CelType.Bool, A, A], A),
        Global("_==_", "equals", [A, A], CelType.Bool),
        Global("_+_", "add_int64", [CelType.Int, CelType.Int], CelType.Int),
        Global("_+_", "add_list", [CelType.ListOf(A), CelType.ListOf(A)], CelType.ListOf(A)),
        Global("_*_", "multiply_int64", [CelType.Int, CelType.Int], CelType.Int),
        Global("_<_", "less_int64", [CelType.Int, CelType.Int], CelType.Bool),
        Global("_<_", "less_int64_double", [CelType.Int, CelType.Double], CelType.Bool, dynOnly: true),
        Global("_[_]", "index_list", [CelType.ListOf(A), CelType.Int], A),
        Global("_[_]", "index_map", [CelType.MapOf(K, V), K], V),
        Global("size", "size_string", [CelType.String], CelType.Int),
        Global("size", "size_list", [CelType.ListOf(A)], CelType.Int),
        Receiver("size", "string_size", CelType.String, [], CelType.Int),
        Global("type", "type", [A], CelType.TypeOf(A)),
        Global("dyn", "to_dyn", [A], CelType.Dyn),
    ];

    private static readonly FrozenDictionary<string, CelType> TypeIdentifiers = new Dictionary<string, CelType>(StringComparer.Ordinal)
    {
        ["int"] = CelType.Int,
        ["string"] = CelType.String,
        ["list"] = CelType.ListOf(CelType.Dyn),
        ["type"] = CelType.TypeOf(CelType.Dyn),
    }.ToFrozenDictionary(StringComparer.Ordinal);

    // The library above with the overloads and variables of a test, in the container.
    internal static DeclarationIndex Create(ImmutableArray<OverloadBinding> functions, ImmutableArray<CelVariableDecl> variables, string container = "") =>
        DeclarationIndex.Create([.. Library, .. functions], variables, TypeIdentifiers, container);

    internal static OverloadBinding Global(string function, string overloadId, ImmutableArray<CelType> parameterTypes, CelType resultType, bool dynOnly = false) =>
        new(function, new CelOverloadDecl(overloadId, false, parameterTypes, resultType), null, 1, null, "test", dynOnly: dynOnly);

    internal static OverloadBinding Receiver(string function, string overloadId, CelType receiverType, ImmutableArray<CelType> parameterTypes, CelType resultType) =>
        new(function, new CelOverloadDecl(overloadId, true, [receiverType, .. parameterTypes], resultType), null, 1, null, "test");

    internal static ParsedExpression Parse(string expression, SyntaxOptions? syntax = null)
    {
        ParseResult parsed = Parser.Parse(expression, syntax);
        Assert.True(parsed.IsSuccess, expression);
        return parsed.Expression;
    }
}

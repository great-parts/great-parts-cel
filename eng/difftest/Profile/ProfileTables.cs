using System.Collections.Generic;

namespace Great.Parts.Cel.Eng.Difftest.Profile;

/// <summary>A variable of the run manifest.</summary>
/// <param name="Name">The name.</param>
/// <param name="Type">The declared type.</param>
internal sealed record ProfileVariable(string Name, ProfileType Type);

/// <summary>An overload of the profile.</summary>
/// <param name="Id">The overload identifier of cel-go, for example <c>add_int64</c>.</param>
/// <param name="Function">The function, for example <c>_+_</c>.</param>
/// <param name="Parameters">The parameter types, the receiver first.</param>
/// <param name="Result">The result type.</param>
internal sealed record ProfileOverload(string Id, string Function, IReadOnlyList<ProfileType> Parameters, ProfileType Result);

/// <summary>
/// The tables of the profile of the walking skeleton (docs/design/eng-difftest.md, "Profile of the walking skeleton"): the
/// variables of the manifest, the functions and their overloads, and the limits of a generated case.
/// </summary>
internal static class ProfileTables
{
    /// <summary>The path of the run manifest from the repository root.</summary>
    internal const string ManifestPath = "eng/difftest/Profile/environment.json";

    /// <summary>
    /// The path of the time zone data file from the repository root, whose zones the run compares (<c>ENG822</c>). The profile
    /// of the walking skeleton has no time zone data file: the path is empty, and runs do not compare offsets.
    /// </summary>
    internal const string ZoneDataPath = "";

    /// <summary>The function <c>_+_</c>.</summary>
    internal const string Add = "_+_";

    /// <summary>The function <c>_/_</c>.</summary>
    internal const string Divide = "_/_";

    /// <summary>The function <c>-_</c>.</summary>
    internal const string Negate = "-_";

    /// <summary>The function <c>_||_</c>.</summary>
    internal const string LogicalOr = "_||_";

    /// <summary>The largest depth of an expression; a literal or an identifier alone has depth 1.</summary>
    internal const int MaxDepth = 6;

    /// <summary>The largest number of nodes of an expression: literals, identifiers, calls, lists, maps, and keys and values of entries.</summary>
    internal const int MaxNodes = 40;

    /// <summary>The largest number of Unicode scalar values of a string, in a literal or a binding.</summary>
    internal const int MaxCodePoints = 8;

    /// <summary>The largest number of bytes of a <c>bytes</c> value, in a literal or a binding.</summary>
    internal const int MaxBytes = 8;

    /// <summary>The largest number of elements of a list literal and of entries of a map literal.</summary>
    internal const int MaxElements = 3;

    /// <summary>The variables of the manifest, in its order.</summary>
    internal static IReadOnlyList<ProfileVariable> Variables { get; } =
    [
        new("i1", ProfileType.IntType),
        new("i2", ProfileType.IntType),
        new("u1", ProfileType.UintType),
        new("d1", ProfileType.DoubleType),
        new("b1", ProfileType.BoolType),
        new("b2", ProfileType.BoolType),
        new("s1", ProfileType.StringType),
        new("y1", ProfileType.BytesType),
        new("x1", ProfileType.DynType),
        new("x2", ProfileType.DynType),
    ];

    /// <summary>The overloads of the profile.</summary>
    internal static IReadOnlyList<ProfileOverload> Overloads { get; } =
    [
        new("add_int64", Add, [ProfileType.IntType, ProfileType.IntType], ProfileType.IntType),
        new("divide_int64", Divide, [ProfileType.IntType, ProfileType.IntType], ProfileType.IntType),
        new("negate_int64", Negate, [ProfileType.IntType], ProfileType.IntType),
        new("logical_or", LogicalOr, [ProfileType.BoolType, ProfileType.BoolType], ProfileType.BoolType),
    ];

    /// <summary>The types for which the generator builds an expression: the root, list elements and map values take any of them.</summary>
    internal static IReadOnlyList<ProfileType> TargetTypes { get; } =
    [
        ProfileType.IntType,
        ProfileType.UintType,
        ProfileType.DoubleType,
        ProfileType.BoolType,
        ProfileType.StringType,
        ProfileType.BytesType,
        ProfileType.NullType,
        ProfileType.ListType,
        ProfileType.MapType,
    ];

    /// <summary>The types of map keys.</summary>
    internal static IReadOnlyList<ProfileType> KeyTypes { get; } =
        [ProfileType.IntType, ProfileType.UintType, ProfileType.BoolType, ProfileType.StringType];

    /// <summary>The kinds of the value of a <c>dyn</c> variable: the scalar kinds and <c>null</c>.</summary>
    internal static IReadOnlyList<ProfileType> DynKinds { get; } =
    [
        ProfileType.IntType,
        ProfileType.UintType,
        ProfileType.DoubleType,
        ProfileType.BoolType,
        ProfileType.StringType,
        ProfileType.BytesType,
        ProfileType.NullType,
    ];
}

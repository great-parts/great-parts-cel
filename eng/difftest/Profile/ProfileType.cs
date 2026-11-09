using System;

namespace Great.Parts.Cel.Eng.Difftest.Profile;

/// <summary>
/// A type of the profile: a target type of the generator, the type of a variable or a parameter of an overload. Members end
/// with <c>Type</c>, so that no name is the name of a .NET type. <c>list</c>, <c>map</c> and <c>type</c> stand without
/// parameters; <c>timestamp</c> and <c>duration</c> are the short names of the standard library page; the type parameters <c>A</c> and <c>B</c> occur only as parameters and results of overloads.
/// </summary>
internal enum ProfileType
{
    /// <summary><c>int</c>.</summary>
    IntType,

    /// <summary><c>uint</c>.</summary>
    UintType,

    /// <summary><c>double</c>.</summary>
    DoubleType,

    /// <summary><c>bool</c>.</summary>
    BoolType,

    /// <summary><c>string</c>.</summary>
    StringType,

    /// <summary><c>bytes</c>.</summary>
    BytesType,

    /// <summary><c>null_type</c>.</summary>
    NullType,

    /// <summary><c>list</c>, without parameters.</summary>
    ListType,

    /// <summary><c>map</c>, without parameters.</summary>
    MapType,

    /// <summary><c>type</c>: the type of type values, without parameters.</summary>
    TypeType,

    /// <summary><c>timestamp</c>: <c>google.protobuf.Timestamp</c>; the profile makes its values in the expression.</summary>
    TimestampType,

    /// <summary><c>duration</c>: <c>google.protobuf.Duration</c>; the profile makes its values in the expression.</summary>
    DurationType,

    /// <summary><c>dyn</c>: the type of a variable whose value has any kind.</summary>
    DynType,

    /// <summary>The type parameter <c>A</c> of an overload: it takes a value of any type.</summary>
    ParameterAType,

    /// <summary>The type parameter <c>B</c> of an overload: it takes a value of any type.</summary>
    ParameterBType,
}

/// <summary>The CEL names of the types of the profile.</summary>
internal static class ProfileTypeNames
{
    /// <summary>
    /// The CEL name of a type, as <c>type()</c> gives it and as the manifest writes it: <c>int</c>, <c>null_type</c>,
    /// <c>dyn</c>; a type parameter is <c>A</c> or <c>B</c>.
    /// </summary>
    /// <param name="type">The type.</param>
    internal static string Name(this ProfileType type) => type switch
    {
        ProfileType.IntType => "int",
        ProfileType.UintType => "uint",
        ProfileType.DoubleType => "double",
        ProfileType.BoolType => "bool",
        ProfileType.StringType => "string",
        ProfileType.BytesType => "bytes",
        ProfileType.NullType => "null_type",
        ProfileType.ListType => "list",
        ProfileType.MapType => "map",
        ProfileType.TypeType => "type",
        ProfileType.TimestampType => "timestamp",
        ProfileType.DurationType => "duration",
        ProfileType.DynType => "dyn",
        ProfileType.ParameterAType => "A",
        ProfileType.ParameterBType => "B",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Not a type of the profile."),
    };

    /// <summary>Whether a type is a type parameter of an overload.</summary>
    /// <param name="type">The type.</param>
    internal static bool IsParameter(this ProfileType type) => type is ProfileType.ParameterAType or ProfileType.ParameterBType;

    /// <summary>Whether a type is <c>timestamp</c> or <c>duration</c>: a type with no literal, whose values the constructors make.</summary>
    /// <param name="type">The type.</param>
    internal static bool IsTime(this ProfileType type) => type is ProfileType.TimestampType or ProfileType.DurationType;
}

using System;

namespace Great.Parts.Cel.Values;

/// <summary>
/// The names of the CEL types of values by their kinds, as messages of errors write them; a value of the
/// kind <see cref="CelValueKind.Error"/> has the type <c>error</c>, the name of the error type in cel-go.
/// </summary>
internal static class ValueTypeNames
{
    /// <summary>
    /// Gives the name of the CEL type of a value of <paramref name="kind"/>, for the kinds that evaluation
    /// produces so far.
    /// </summary>
    /// <param name="kind">The kind of the value.</param>
    /// <returns>The name of the type, such as <c>null_type</c> or <c>list</c>.</returns>
    internal static string Of(CelValueKind kind) => kind switch
    {
        CelValueKind.Null => "null_type",
        CelValueKind.Bool => "bool",
        CelValueKind.Int => "int",
        CelValueKind.UInt => "uint",
        CelValueKind.Double => "double",
        CelValueKind.String => "string",
        CelValueKind.Bytes => "bytes",
        CelValueKind.List => "list",
        CelValueKind.Map => "map",
        CelValueKind.Type => "type",
        CelValueKind.Error => "error",
        _ => throw new NotImplementedException(),
    };
}

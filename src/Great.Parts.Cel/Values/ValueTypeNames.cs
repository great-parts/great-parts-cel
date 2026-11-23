using System.Diagnostics;

namespace Great.Parts.Cel.Values;

/// <summary>
/// The names of the CEL types of values by their kinds, as messages of errors write them; a value of the
/// kind <see cref="CelValueKind.Error"/> has the type <c>error</c>, the name of the error type in cel-go.
/// </summary>
internal static class ValueTypeNames
{
    /// <summary>
    /// Gives the name of the CEL type of a value of <paramref name="kind"/>, for the kinds that the kind alone names. The kinds
    /// <see cref="CelValueKind.Object"/> and <see cref="CelValueKind.Abstract"/> are named by the description or the type of the
    /// value, as <see cref="ValueTypeRules.NameOf"/> does.
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
        CelValueKind.Timestamp => "timestamp",
        CelValueKind.Duration => "duration",
        CelValueKind.Optional => "optional_type",
        CelValueKind.Error => "error",
        CelValueKind.Decimal => "decimal",
        CelValueKind.Date => "date",
        CelValueKind.TimeOfDay => "time_of_day",
        _ => throw new UnreachableException("The kind has no name of its own: an object or an abstract value is named by its type."),
    };
}

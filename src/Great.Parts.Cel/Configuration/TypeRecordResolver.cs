using System.Collections.Generic;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Configuration;

/// <summary>
/// Turns a type record of the environment description into a <see cref="CelType"/>: the simple types, <c>dyn</c> and
/// <c>null</c>, the wrappers, <c>any</c>, <c>list</c>, <c>map</c> and <c>type</c>, a type parameter where an overload takes
/// it, and any other name as the type of that full name: the CEL type of a well-known message of protobuf, or an object type.
/// </summary>
/// <remarks>
/// A record names its type exactly, so the container of the environment does not apply to it. The types that the library
/// has not brought yet are errors that name the type. The records are resolved with an explicit stack, so the nesting of
/// a record has no limit other than the length of the text.
/// </remarks>
internal static class TypeRecordResolver
{
    /// <summary>
    /// Resolves a type record in full.
    /// </summary>
    /// <param name="text">The text of the record, such as <c>map&lt;string, list&lt;acme.Order&gt;&gt;</c>.</param>
    /// <param name="allowTypeParameters">Whether the record may use type parameters, as the records of an overload do.</param>
    /// <param name="subject">What the record is the type of, for the message of a misplaced type parameter, such as <c>a variable</c>.</param>
    /// <param name="detail">What is wrong with the record, the end of the message of the error; empty when it resolves.</param>
    /// <returns>The type, or <see langword="null"/> when the record is wrong.</returns>
    internal static CelType? Resolve(string text, bool allowTypeParameters, string subject, out string detail)
    {
        detail = "";
        if (TypeRecordParser.ParseTokens(text) is not { } tokens)
        {
            detail = $"'{text}' is not a valid type record";
            return null;
        }

        Stack<(TypeRecord Record, List<CelType> Parameters)> open = new();
        CelType? result = null;
        foreach (TypeToken token in tokens)
        {
            if (token.Type.HasParameters)
            {
                open.Push((token.Type, []));
                continue;
            }

            // The type goes to the list it ends, which then is a type of its own, and so on for each closing bracket.
            CelType? type = Leaf(token.Type, text, allowTypeParameters, subject, out detail);
            for (int closed = 0; type is not null && closed < token.Closed; closed++)
            {
                open.Peek().Parameters.Add(type);
                (TypeRecord record, List<CelType> parameters) = open.Pop();
                type = Parametrized(record, parameters, text, out detail);
            }

            if (type is null)
            {
                return null;
            }

            Add(open, type, ref result);
        }

        return result;
    }

    private static void Add(Stack<(TypeRecord Record, List<CelType> Parameters)> open, CelType type, ref CelType? result)
    {
        if (open.Count > 0)
        {
            open.Peek().Parameters.Add(type);
        }
        else
        {
            result = type;
        }
    }

    // The type of a name or of a type parameter that has no parameters in angle brackets.
    private static CelType? Leaf(TypeRecord record, string text, bool allowTypeParameters, string subject, out string detail)
    {
        detail = "";
        if (record.IsTypeParameter)
        {
            if (allowTypeParameters)
            {
                return CelType.TypeParameter(record.Name);
            }

            detail = $"a type parameter cannot be used in {subject}: '{text}'";
            return null;
        }

        CelType? simple = Simple(record.Name);
        if (simple is not null)
        {
            return simple;
        }

        string name = Bare(record.Name);
        switch (name)
        {
            case "type":
                return CelType.TypeOf(CelType.Dyn);
            case "list":
            case "map":
            case "optional_type":
                detail = $"the type '{name}' takes {(name == "map" ? "2 type parameters" : "1 type parameter")}: '{text}'";
                return null;
            case "any":
                return CelType.Any;
            default:
                return CelType.ObjectNamed(name);
        }
    }

    // The type of a name with its parameters in angle brackets.
    private static CelType? Parametrized(TypeRecord record, List<CelType> parameters, string text, out string detail)
    {
        detail = "";
        string name = Bare(record.Name);
        switch (name)
        {
            case "optional_type" when parameters.Count == 1:
                return CelType.OptionalOf(parameters[0]);
            case "list" when parameters.Count == 1:
                return CelType.ListOf(parameters[0]);
            case "map" when parameters.Count == 2:
                return CelType.MapOf(parameters[0], parameters[1]);
            case "type" when parameters.Count == 1:
                return CelType.TypeOf(parameters[0]);
            case "list" or "map" or "type" or "optional_type":
                detail = $"the type '{name}' takes {Expected(name)}, but '{text}' gives {parameters.Count}";
                return null;
            default:
                detail = $"the type '{name}' has no type parameters: '{text}'";
                return null;
        }
    }

    private static string Expected(string name) => name switch
    {
        "map" => "2 type parameters",
        _ => "1 type parameter",
    };

    private static string Bare(string name) => name is ['.', ..] ? name[1..] : name;

    private static CelType? Simple(string name) => name switch
    {
        "int" => CelType.Int,
        "uint" => CelType.UInt,
        "double" => CelType.Double,
        "bool" => CelType.Bool,
        "string" => CelType.String,
        "bytes" => CelType.Bytes,
        "dyn" => CelType.Dyn,
        "null_type" or "null" => CelType.Null,
        "timestamp" => CelType.Timestamp,
        "duration" => CelType.Duration,
        "int_wrapper" => CelType.WrapperOf(CelType.Int),
        "uint_wrapper" => CelType.WrapperOf(CelType.UInt),
        "double_wrapper" => CelType.WrapperOf(CelType.Double),
        "bool_wrapper" => CelType.WrapperOf(CelType.Bool),
        "string_wrapper" => CelType.WrapperOf(CelType.String),
        "bytes_wrapper" => CelType.WrapperOf(CelType.Bytes),
        _ => null,
    };
}

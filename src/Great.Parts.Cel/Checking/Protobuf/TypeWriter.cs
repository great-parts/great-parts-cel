using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Checking;

// Writes a CEL type as the Type message of checked.proto: primitives, wrappers and well-known types by their enumerations, list, map,
// message, type parameter and type by their fields. A type of this library that checked.proto has no case for is the abstract type
// of its name, as cel-go writes optional_type: optional_type(T), decimal and the abstract types of the extension sets.
internal static class TypeWriter
{
    private const string OptionalName = "optional_type";
    private const string DecimalName = "decimal";

    /// <summary>
    /// Writes a type as a field of a message.
    /// </summary>
    /// <param name="output">The writer.</param>
    /// <param name="field">The number of the field.</param>
    /// <param name="type">The type.</param>
    internal static void Write(ProtoWriter output, int field, CelType type)
    {
        int token = output.Begin(field, out int start);
        WriteBody(output, type);
        output.End(token, start);
    }

    private static void WriteBody(ProtoWriter output, CelType type)
    {
        CelTypeKind kind = type.Kind;
        int primitive = Primitive(kind);
        if (primitive != 0)
        {
            output.Int64Always(3, primitive);
            return;
        }

        int wellKnown = WellKnown(kind);
        if (wellKnown != 0)
        {
            output.Int64Always(5, wellKnown);
            return;
        }

        switch (kind)
        {
            case CelTypeKind.Null:
                output.Int64Always(2, 0);
                break;
            case CelTypeKind.Wrapper:
                output.Int64Always(4, type.Parameters.IsEmpty ? 0 : Primitive(type.Parameters[0].Kind));
                break;
            case CelTypeKind.List:
            case CelTypeKind.Map:
                WriteCollection(output, type);
                break;
            default:
                WriteNamed(output, type);
                break;
        }
    }

    // The types that are a name or a type of their own: an object, a type parameter, type(T), and the types with no case of their own.
    private static void WriteNamed(ProtoWriter output, CelType type)
    {
        switch (type.Kind)
        {
            case CelTypeKind.Object:
                output.StringAlways(9, type.Name);
                break;
            case CelTypeKind.TypeParameter:
                output.StringAlways(10, type.Name);
                break;
            case CelTypeKind.Type when !type.Parameters.IsEmpty:
                Write(output, 11, type.Parameters[0]);
                break;
            case CelTypeKind.Type:
                EmptyMessage(output, 11);
                break;
            case CelTypeKind.Optional:
            case CelTypeKind.Decimal:
            case CelTypeKind.Abstract:
                WriteAbstract(output, type);
                break;
            default:
                EmptyMessage(output, 1);
                break;
        }
    }

    // list_type { elem_type } and map_type { key_type, value_type }.
    private static void WriteCollection(ProtoWriter output, CelType type)
    {
        int token = output.Begin(type.Kind == CelTypeKind.List ? 6 : 7, out int start);
        for (int i = 0; i < type.Parameters.Length; i++)
        {
            Write(output, i + 1, type.Parameters[i]);
        }

        output.End(token, start);
    }

    // abstract_type { name, parameter_types }.
    private static void WriteAbstract(ProtoWriter output, CelType type)
    {
        int token = output.Begin(14, out int start);
        output.String(1, type.Kind switch
        {
            CelTypeKind.Optional => OptionalName,
            CelTypeKind.Decimal => DecimalName,
            _ => type.Name,
        });
        foreach (CelType parameter in type.Parameters)
        {
            Write(output, 2, parameter);
        }

        output.End(token, start);
    }

    // The empty message that the Type message of dyn and of a type without a parameter holds.
    private static void EmptyMessage(ProtoWriter output, int field)
    {
        int token = output.Begin(field, out int start);
        output.End(token, start);
    }

    // The value of the enumeration WellKnownType: ANY 1, TIMESTAMP 2, DURATION 3; 0 for any other type.
    private static int WellKnown(CelTypeKind kind) => kind switch
    {
        CelTypeKind.Any => 1,
        CelTypeKind.Timestamp => 2,
        CelTypeKind.Duration => 3,
        _ => 0,
    };

    // The value of the enumeration PrimitiveType: BOOL 1, INT64 2, UINT64 3, DOUBLE 4, STRING 5, BYTES 6; 0 for any other type.
    private static int Primitive(CelTypeKind kind) => kind switch
    {
        CelTypeKind.Bool => 1,
        CelTypeKind.Int => 2,
        CelTypeKind.UInt => 3,
        CelTypeKind.Double => 4,
        CelTypeKind.String => 5,
        CelTypeKind.Bytes => 6,
        _ => 0,
    };
}

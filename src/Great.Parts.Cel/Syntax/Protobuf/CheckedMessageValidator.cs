using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel.Syntax;

// Reads the reference_map and the type_map of a CheckedExpr message without building anything, so that a wire error in them is
// InvalidProtobuf as in the other fields: loading a checked expression uses only its expr and source_info, and compiles the tree
// again. The messages are those of checked.proto: Reference (name, overload_id, value) and Type.
internal static class CheckedMessageValidator
{
    // The depth of types inside types that is read: as deep as an expression nests.
    private const int MaxTypeDepth = ExprReader.MaxDepth;

    // An entry of reference_map: the id and a Reference.
    internal static void ValidateReferenceEntry(ProtoReader entry)
    {
        while (entry.TryReadField(out int field, out int wireType))
        {
            switch (field)
            {
                case 1:
                    _ = entry.ReadInt64(wireType);
                    break;
                case 2:
                    ValidateReference(entry.ReadMessage(wireType));
                    break;
                default:
                    entry.Skip(wireType);
                    break;
            }
        }
    }

    // An entry of type_map: the id and a Type.
    internal static void ValidateTypeEntry(ProtoReader entry)
    {
        while (entry.TryReadField(out int field, out int wireType))
        {
            switch (field)
            {
                case 1:
                    _ = entry.ReadInt64(wireType);
                    break;
                case 2:
                    ValidateType(entry.ReadMessage(wireType), 0);
                    break;
                default:
                    entry.Skip(wireType);
                    break;
            }
        }
    }

    private static void ValidateReference(ProtoReader reference)
    {
        while (reference.TryReadField(out int field, out int wireType))
        {
            switch (field)
            {
                case 1 or 3:
                    _ = reference.ReadString(wireType);
                    break;
                case 4:
                    _ = ExprReader.ReadConstant(reference.ReadMessage(wireType));
                    break;
                default:
                    reference.Skip(wireType);
                    break;
            }
        }
    }

    private static void ValidateType(ProtoReader type, int depth)
    {
        if (depth > MaxTypeDepth)
        {
            throw ProtoErrors.Stop(CelErrorCode.NestingTooDeep, "type nesting exceeds the limit of 250");
        }

        while (type.TryReadField(out int field, out int wireType))
        {
            if (field is >= 2 and <= 5)
            {
                _ = type.ReadInt64(wireType);
            }
            else if (field is 9 or 10)
            {
                _ = type.ReadString(wireType);
            }
            else if (!ValidateNestedType(ref type, field, wireType, depth))
            {
                type.Skip(wireType);
            }
        }
    }

    // The members of the oneof type_kind that hold a message: list_type, map_type, function, type and abstract_type.
    private static bool ValidateNestedType(ref ProtoReader type, int field, int wireType, int depth)
    {
        switch (field)
        {
            case 6 or 7 or 8:
                ValidateTypeParts(type.ReadMessage(wireType), depth + 1, nameField: 0);
                return true;
            case 14:
                ValidateTypeParts(type.ReadMessage(wireType), depth + 1, nameField: 1);
                return true;
            case 11:
                ValidateType(type.ReadMessage(wireType), depth + 1);
                return true;
            default:
                return false;
        }
    }

    // The message of list_type, map_type, function or abstract_type: fields 1 and 2 are types, except the name of an abstract type.
    private static void ValidateTypeParts(ProtoReader parts, int depth, int nameField)
    {
        while (parts.TryReadField(out int field, out int wireType))
        {
            if (field == nameField)
            {
                _ = parts.ReadString(wireType);
            }
            else if (field is 1 or 2)
            {
                ValidateType(parts.ReadMessage(wireType), depth);
            }
            else
            {
                parts.Skip(wireType);
            }
        }
    }
}

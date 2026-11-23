using System;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Syntax;

// Reads the Expr message of syntax.proto into ExprData: the fields by number, an unknown field skipped, a known field of the wrong
// wire type InvalidProtobuf. A later member of the oneof replaces an earlier one. Expr inside Expr nests no deeper than the limit of
// parsing (NestingTooDeep), so the recursion of the reader is bounded whatever the bytes are.
internal static class ExprReader
{
    internal const int MaxDepth = 250;

    internal static ExprData ReadExpr(ProtoReader reader, int depth)
    {
        if (depth > MaxDepth)
        {
            throw ProtoErrors.Stop(CelErrorCode.NestingTooDeep, "syntax tree depth exceeds the limit of 250");
        }

        var data = new ExprData();
        while (reader.TryReadField(out int field, out int wireType))
        {
            if (field == 2)
            {
                data.Id = reader.ReadInt64(wireType);
            }
            else if (!ReadKind(ref reader, data, field, wireType, depth))
            {
                reader.Skip(wireType);
            }
        }

        return data;
    }

    // The member of the oneof expr_kind with the field number; false when the number is not one of them.
    private static bool ReadKind(ref ProtoReader reader, ExprData data, int field, int wireType, int depth)
    {
        if (field is < 3 or > 9)
        {
            return false;
        }

        ProtoReader message = reader.ReadMessage(wireType);
        data.Reset();
        switch (field)
        {
            case 3:
                data.Case = ExprCase.Constant;
                data.Value = ReadConstant(message);
                break;
            case 4:
                data.Case = ExprCase.Ident;
                ReadIdent(message, data);
                break;
            case 5:
                data.Case = ExprCase.Select;
                ReadSelect(message, data, depth);
                break;
            case 6:
                data.Case = ExprCase.Call;
                ReadCall(message, data, depth);
                break;
            case 7:
                data.Case = ExprCase.List;
                ReadList(message, data, depth);
                break;
            case 8:
                data.Case = ExprCase.Struct;
                ReadStruct(message, data, depth);
                break;
            default:
                data.Case = ExprCase.Comprehension;
                ReadComprehension(message, data, depth);
                break;
        }

        return true;
    }

    // The value of the oneof constant_kind, or null when the message sets none.
    internal static CelValue? ReadConstant(ProtoReader reader)
    {
        CelValue? value = null;
        while (reader.TryReadField(out int field, out int wireType))
        {
            switch (field)
            {
                case 1:
                    _ = reader.ReadInt64(wireType);
                    value = CelValue.Null;
                    break;
                case 2:
                    value = CelValue.FromBoolean(reader.ReadBool(wireType));
                    break;
                case 3:
                    value = CelValue.FromInt64(reader.ReadInt64(wireType));
                    break;
                case 4:
                    value = CelValue.FromUInt64((ulong)reader.ReadInt64(wireType));
                    break;
                case 5:
                    value = CelValue.FromDouble(reader.ReadDouble(wireType));
                    break;
                case 6:
                    value = CelValue.FromString(reader.ReadString(wireType));
                    break;
                case 7:
                    value = CelValue.FromBytes(reader.ReadBytes(wireType).ToArray());
                    break;
                case 8:
                    value = ReadDuration(reader.ReadMessage(wireType));
                    break;
                case 9:
                    value = ReadTimestamp(reader.ReadMessage(wireType));
                    break;
                default:
                    reader.Skip(wireType);
                    break;
            }
        }

        return value;
    }

    // google.protobuf.Duration: seconds and nanos; a pair that is not a duration is a tree that breaks the rules of the message.
    private static CelValue ReadDuration(ProtoReader reader)
    {
        (long seconds, int nanos) = ReadSecondsAndNanos(reader);
        try
        {
            return CelValue.FromDuration(new CelDuration(seconds, nanos));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw ProtoErrors.Stop(CelErrorCode.InvalidSyntaxTree, "a duration constant is out of range: " + exception.Message);
        }
    }

    private static CelValue ReadTimestamp(ProtoReader reader)
    {
        (long seconds, int nanos) = ReadSecondsAndNanos(reader);
        try
        {
            return CelValue.FromTimestamp(new CelTimestamp(seconds, nanos));
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw ProtoErrors.Stop(CelErrorCode.InvalidSyntaxTree, "a timestamp constant is out of range: " + exception.Message);
        }
    }

    private static (long Seconds, int Nanos) ReadSecondsAndNanos(ProtoReader reader)
    {
        long seconds = 0;
        int nanos = 0;
        while (reader.TryReadField(out int field, out int wireType))
        {
            switch (field)
            {
                case 1:
                    seconds = reader.ReadInt64(wireType);
                    break;
                case 2:
                    nanos = reader.ReadInt32(wireType);
                    break;
                default:
                    reader.Skip(wireType);
                    break;
            }
        }

        return (seconds, nanos);
    }

    private static void ReadIdent(ProtoReader reader, ExprData data)
    {
        while (reader.TryReadField(out int field, out int wireType))
        {
            if (field == 1)
            {
                data.Name = reader.ReadString(wireType);
            }
            else
            {
                reader.Skip(wireType);
            }
        }
    }

    private static void ReadSelect(ProtoReader reader, ExprData data, int depth)
    {
        while (reader.TryReadField(out int field, out int wireType))
        {
            switch (field)
            {
                case 1:
                    data.First = ReadExpr(reader.ReadMessage(wireType), depth + 1);
                    break;
                case 2:
                    data.Name = reader.ReadString(wireType);
                    break;
                case 3:
                    data.TestOnly = reader.ReadBool(wireType);
                    break;
                default:
                    reader.Skip(wireType);
                    break;
            }
        }
    }

    private static void ReadCall(ProtoReader reader, ExprData data, int depth)
    {
        while (reader.TryReadField(out int field, out int wireType))
        {
            switch (field)
            {
                case 1:
                    data.First = ReadExpr(reader.ReadMessage(wireType), depth + 1);
                    break;
                case 2:
                    data.Name = reader.ReadString(wireType);
                    break;
                case 3:
                    data.Items.Add(ReadExpr(reader.ReadMessage(wireType), depth + 1));
                    break;
                default:
                    reader.Skip(wireType);
                    break;
            }
        }
    }

    private static void ReadList(ProtoReader reader, ExprData data, int depth)
    {
        while (reader.TryReadField(out int field, out int wireType))
        {
            switch (field)
            {
                case 1:
                    data.Items.Add(ReadExpr(reader.ReadMessage(wireType), depth + 1));
                    break;
                case 2:
                    reader.ReadInt32s(wireType, data.OptionalIndices);
                    break;
                default:
                    reader.Skip(wireType);
                    break;
            }
        }
    }

    private static void ReadStruct(ProtoReader reader, ExprData data, int depth)
    {
        while (reader.TryReadField(out int field, out int wireType))
        {
            switch (field)
            {
                case 1:
                    data.Name = reader.ReadString(wireType);
                    break;
                case 2:
                    data.Entries.Add(ReadEntry(reader.ReadMessage(wireType), depth));
                    break;
                default:
                    reader.Skip(wireType);
                    break;
            }
        }
    }

    // An entry: a later key replaces an earlier one, whichever kind it is (the oneof key_kind).
    private static EntryData ReadEntry(ProtoReader reader, int depth)
    {
        var entry = new EntryData();
        while (reader.TryReadField(out int field, out int wireType))
        {
            switch (field)
            {
                case 1:
                    entry.Id = reader.ReadInt64(wireType);
                    break;
                case 2:
                    entry.FieldKey = reader.ReadString(wireType);
                    entry.MapKey = null;
                    break;
                case 3:
                    entry.MapKey = ReadExpr(reader.ReadMessage(wireType), depth + 1);
                    entry.FieldKey = null;
                    break;
                case 4:
                    entry.Value = ReadExpr(reader.ReadMessage(wireType), depth + 1);
                    break;
                case 5:
                    entry.IsOptional = reader.ReadBool(wireType);
                    break;
                default:
                    reader.Skip(wireType);
                    break;
            }
        }

        return entry;
    }

    private static void ReadComprehension(ProtoReader reader, ExprData data, int depth)
    {
        while (reader.TryReadField(out int field, out int wireType))
        {
            switch (field)
            {
                case 1:
                    data.Name = reader.ReadString(wireType);
                    break;
                case 8:
                    data.SecondName = reader.ReadString(wireType);
                    break;
                case 3:
                    data.AccuName = reader.ReadString(wireType);
                    break;
                default:
                    if (!ReadComprehensionPart(ref reader, data, field, wireType, depth))
                    {
                        reader.Skip(wireType);
                    }

                    break;
            }
        }
    }

    // The expressions of a comprehension: the range, the initial accumulator, the condition, the step and the result.
    private static bool ReadComprehensionPart(ref ProtoReader reader, ExprData data, int field, int wireType, int depth)
    {
        if (field is < 2 or > 7)
        {
            return false;
        }

        ExprData part = ReadExpr(reader.ReadMessage(wireType), depth + 1);
        switch (field)
        {
            case 2:
                data.First = part;
                break;
            case 4:
                data.AccuInit = part;
                break;
            case 5:
                data.Condition = part;
                break;
            case 6:
                data.Step = part;
                break;
            default:
                data.Result = part;
                break;
        }

        return true;
    }
}

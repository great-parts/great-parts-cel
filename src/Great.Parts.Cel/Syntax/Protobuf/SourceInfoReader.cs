namespace Great.Parts.Cel.Syntax;

// Reads the SourceInfo message of syntax.proto into SourceData: the location, the starts of the lines, the position of each id and the
// macro calls by the id of the expansion. The version and the extensions are not used and are skipped.
internal static class SourceInfoReader
{
    internal static SourceData Read(ProtoReader reader)
    {
        var source = new SourceData();
        while (reader.TryReadField(out int field, out int wireType))
        {
            switch (field)
            {
                case 1:
                    _ = reader.ReadString(wireType);
                    break;
                case 2:
                    source.Location = reader.ReadString(wireType);
                    break;
                case 3:
                    reader.ReadInt32s(wireType, source.LineOffsets);
                    break;
                case 4:
                    ReadPosition(reader.ReadMessage(wireType), source);
                    break;
                case 5:
                    ReadMacroCall(reader.ReadMessage(wireType), source);
                    break;
                default:
                    reader.Skip(wireType);
                    break;
            }
        }

        return source;
    }

    // An entry of positions: the id and the offset in code points; a missing one is 0.
    private static void ReadPosition(ProtoReader entry, SourceData source)
    {
        long id = 0;
        int position = 0;
        while (entry.TryReadField(out int field, out int wireType))
        {
            switch (field)
            {
                case 1:
                    id = entry.ReadInt64(wireType);
                    break;
                case 2:
                    position = entry.ReadInt32(wireType);
                    break;
                default:
                    entry.Skip(wireType);
                    break;
            }
        }

        source.Positions[id] = position;
    }

    // An entry of macro_calls: the id of the expansion and the call, which has its own depth of nesting.
    private static void ReadMacroCall(ProtoReader entry, SourceData source)
    {
        long id = 0;
        ExprData call = new();
        while (entry.TryReadField(out int field, out int wireType))
        {
            switch (field)
            {
                case 1:
                    id = entry.ReadInt64(wireType);
                    break;
                case 2:
                    call = ExprReader.ReadExpr(entry.ReadMessage(wireType), 0);
                    break;
                default:
                    entry.Skip(wireType);
                    break;
            }
        }

        source.MacroCalls[id] = call;
    }
}

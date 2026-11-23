using System;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel.Syntax;

// The messages cel.expr.ParsedExpr and cel.expr.CheckedExpr of cel-spec as the library reads and writes them: the field numbers are
// those of syntax.proto and checked.proto of cel-expr v0.25.1, and the same bytes are the messages of google.api.expr.v1alpha1.
// Writing a parsed expression is here; the checked form of the tree is written by the layer of type checking over the same writer.
internal static class ParsedExprCodec
{
    private const int ParsedExprField = 2;
    private const int ParsedSourceInfoField = 3;
    private const int CheckedReferenceMapField = 2;
    private const int CheckedTypeMapField = 3;
    private const int CheckedExprField = 4;
    private const int CheckedSourceInfoField = 5;
    private const int CheckedExprVersionField = 6;

    /// <summary>
    /// Writes a parsed expression as a ParsedExpr message.
    /// </summary>
    /// <param name="parsed">The parsed expression.</param>
    /// <returns>The bytes.</returns>
    internal static byte[] Write(CelParsedExpr parsed)
    {
        var writer = new ExprWriter(parsed, ExprForm.Parsed);
        return ProtoWriter.Encode(output =>
        {
            writer.Use(output);
            writer.WriteExpr(ParsedExprField, parsed.Root);
            writer.WriteSourceInfo(ParsedSourceInfoField);
        });
    }

    /// <summary>
    /// Loads a parsed expression from the bytes of a ParsedExpr message or, for a CheckedExpr message, from its expr and source_info.
    /// </summary>
    /// <param name="bytes">The bytes of the message.</param>
    /// <param name="sourceText">The text that the positions refer to, or <see langword="null"/>.</param>
    /// <param name="isChecked"><see langword="true"/> for the bytes of a CheckedExpr message.</param>
    /// <returns>The parsed expression, or the error.</returns>
    internal static CelParseResult Read(ReadOnlySpan<byte> bytes, string? sourceText, bool isChecked)
    {
        try
        {
            ExprData? expr = null;
            var source = new SourceData();
            var reader = new ProtoReader(bytes);
            int exprField = isChecked ? CheckedExprField : ParsedExprField;
            int sourceField = isChecked ? CheckedSourceInfoField : ParsedSourceInfoField;
            while (reader.TryReadField(out int field, out int wireType))
            {
                if (field == exprField)
                {
                    expr = ExprReader.ReadExpr(reader.ReadMessage(wireType), 0);
                }
                else if (field == sourceField)
                {
                    source = SourceInfoReader.Read(reader.ReadMessage(wireType));
                }
                else if (isChecked)
                {
                    ReadCheckedField(ref reader, field, wireType);
                }
                else
                {
                    reader.Skip(wireType);
                }
            }

            return new CelParseResult(TreeLoader.Load(expr, source, sourceText));
        }
        catch (CelException exception)
        {
            return new CelParseResult(exception.Errors);
        }
    }

    // The other fields of a CheckedExpr message: the maps are read for their wire errors, the version of the expression is a string.
    private static void ReadCheckedField(ref ProtoReader reader, int field, int wireType)
    {
        switch (field)
        {
            case CheckedReferenceMapField:
                CheckedMessageValidator.ValidateReferenceEntry(reader.ReadMessage(wireType));
                break;
            case CheckedTypeMapField:
                CheckedMessageValidator.ValidateTypeEntry(reader.ReadMessage(wireType));
                break;
            case CheckedExprVersionField:
                _ = reader.ReadString(wireType);
                break;
            default:
                reader.Skip(wireType);
                break;
        }
    }
}

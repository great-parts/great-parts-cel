using System.Text;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// The error values of evaluation, with the messages of the error code table. The errors of the standard
// library functions come through their call sites.
internal static class EvaluationErrors
{
    // Unbound (3009): a variable without a value in the bindings, at its identifier.
    internal static CelValue Unbound(string name, CelSourceLocation location) =>
        Create(CelErrorCode.Unbound, "no value is bound to variable '" + name + "'", location);

    // DuplicateMapKey (3010): a key of a map literal equal to an earlier key, at the literal. The key is
    // written as a literal of its kind.
    internal static CelValue DuplicateMapKey(CelValue key, CelSourceLocation location) =>
        Create(CelErrorCode.DuplicateMapKey, "duplicate map key: " + KeyText(key), location);

    // UnsupportedMapKeyType (3027): a key of a map literal of a kind that cannot be a map key, at the key.
    internal static CelValue UnsupportedMapKeyType(CelValueKind kind, CelSourceLocation location) =>
        Create(CelErrorCode.UnsupportedMapKeyType, "unsupported map key type '" + ValueTypeNames.Of(kind) + "'", location);

    private static CelValue Create(CelErrorCode code, string message, CelSourceLocation location) =>
        CelValue.FromError(new CelError(code, CelErrorStage.Evaluate, message, location, null, null));

    // A key of one of the kinds of map keys, written as a CEL literal: int, uint, bool or string, as the printer of the
    // syntax tree writes a constant.
    private static string KeyText(CelValue key)
    {
        var text = new StringBuilder();
        Printer.AppendConstant(text, key);
        return text.ToString();
    }
}

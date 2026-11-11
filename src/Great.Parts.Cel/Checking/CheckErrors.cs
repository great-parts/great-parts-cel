using System;
using System.Text;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Checking;

// The check errors, with the messages of the error code table. Types are written as in the language
// definition, such as list(int).
internal static class CheckErrors
{
    // UndeclaredReference (2000): a name that is no declaration with any candidate of the container, or an
    // unknown function.
    internal static CelError UndeclaredReference(string name, string container, CelSourceLocation location) =>
        Create(CelErrorCode.UndeclaredReference, "undeclared reference to '" + name + "' (in container '" + container + "')", location);

    // NoMatchingOverload (2001): no overload of a known function accepts the static argument types, the
    // receiver first.
    internal static CelError NoMatchingOverload(string function, ReadOnlySpan<CelType> argumentTypes, CelSourceLocation location)
    {
        var message = new StringBuilder("found no matching overload for '").Append(function).Append("' applied to '(");
        for (int i = 0; i < argumentTypes.Length; i++)
        {
            if (i > 0)
            {
                message.Append(", ");
            }

            message.Append(argumentTypes[i].ToString());
        }

        return Create(CelErrorCode.NoMatchingOverload, message.Append(")'").ToString(), location);
    }

    // TypeMismatch (2002): a part whose type is not assignable to the type that its construct requires.
    internal static CelError TypeMismatch(CelType expected, CelType actual, CelSourceLocation location) =>
        Create(CelErrorCode.TypeMismatch, "expected type '" + expected + "' but found '" + actual + "'", location);

    // ExpectedTypeMismatch (2003): the type of the root is not assignable to the expected type.
    internal static CelError ExpectedTypeMismatch(CelType actual, CelType expected, CelSourceLocation location) =>
        Create(
            CelErrorCode.ExpectedTypeMismatch,
            "expression of type '" + actual + "' is not assignable to the expected type '" + expected + "'",
            location);

    // NoSuchField (2004): a selection on a type without fields, or on an object type without the field.
    internal static CelError NoSuchField(string field, CelType type, CelSourceLocation location) =>
        Create(CelErrorCode.NoSuchField, "undefined field '" + field + "' in type '" + type + "'", location);

    // InvalidComprehensionRange (2005): a range that is not a list, a map or dyn.
    internal static CelError InvalidComprehensionRange(CelType type, CelSourceLocation location) =>
        Create(
            CelErrorCode.InvalidComprehensionRange,
            "expression of type '" + type + "' cannot be the range of a comprehension (must be list, map, or dyn)",
            location);

    // NotAType (2006): the name of a message literal resolves to a declaration that is not an object type, at the literal.
    internal static CelError NotAType(string name, CelSourceLocation location) =>
        Create(CelErrorCode.NotAType, "'" + name + "' is not a type", location);

    // TypeNotConstructible (2008): a message literal of an object type whose description is not constructible, at the
    // literal.
    internal static CelError TypeNotConstructible(string type, CelSourceLocation location) =>
        Create(
            CelErrorCode.TypeNotConstructible,
            "type '" + type + "' cannot be created by an expression: object literals are supported only for protobuf messages",
            location);

    // InvalidRegex (2009): the pattern of matches, a string literal, that the preparation refused. The text is complete: the preparer
    // of the library writes it, the same as the evaluation of a pattern from data. The place is the literal.
    internal static CelError InvalidRegex(string message, CelSourceLocation location) =>
        Create(CelErrorCode.InvalidRegex, message, location);

    // InsufficientStack (9000): the node that checking did not go into.
    internal static CelError InsufficientStack(CelSourceLocation location) =>
        Create(CelErrorCode.InsufficientStack, "insufficient execution stack to continue type checking", location);

    // InternalError (9001): an unexpected exception at the node being checked, kept in CelError.Exception.
    internal static CelError InternalError(Exception exception, CelSourceLocation location) =>
        new(
            CelErrorCode.InternalError,
            CelErrorStage.Check,
            "internal error during type checking; the original exception is in CelError.Exception",
            location,
            null,
            exception);

    private static CelError Create(CelErrorCode code, string message, CelSourceLocation location) =>
        new(code, CelErrorStage.Check, message, location, null, null);
}

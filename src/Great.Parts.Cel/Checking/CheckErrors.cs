using System;
using System.Text;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Checking;

// The check errors, with the messages of the error code table and the parts of the column "Parts": the name and the CEL types. Types
// are written as in the language definition, such as list(int). The place of each is the fragment of its node.
internal static class CheckErrors
{
    // UndeclaredReference (2000): a name that is no declaration with any candidate of the container, or an
    // unknown function.
    internal static CelError UndeclaredReference(string name, string container, CelSourceSpan place) =>
        Create(CelErrorCode.UndeclaredReference, "undeclared reference to '" + name + "' (in container '" + container + "')", place, subject: name);

    // NoMatchingOverload (2001): no overload of a known function accepts the static argument types, the
    // receiver first.
    internal static CelError NoMatchingOverload(string function, ReadOnlySpan<CelType> argumentTypes, CelSourceSpan place)
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

        return Create(CelErrorCode.NoMatchingOverload, message.Append(")'").ToString(), place, subject: function);
    }

    // TypeMismatch (2002): a part whose type is not assignable to the type that its construct requires; the field when the part
    // is the value of a field of a message literal.
    internal static CelError TypeMismatch(CelType expected, CelType actual, CelSourceSpan place, string? field = null) =>
        Create(CelErrorCode.TypeMismatch, "expected type '" + expected + "' but found '" + actual + "'", place, field, expected, actual);

    // ExpectedTypeMismatch (2003): the type of the root is not assignable to the expected type.
    internal static CelError ExpectedTypeMismatch(CelType actual, CelType expected, CelSourceSpan place) =>
        Create(
            CelErrorCode.ExpectedTypeMismatch,
            "expression of type '" + actual + "' is not assignable to the expected type '" + expected + "'",
            place,
            expectedType: expected,
            actualType: actual);

    // NoSuchField (2004): a selection on a type without fields, or on an object type without the field.
    internal static CelError NoSuchField(string field, CelType type, CelSourceSpan place) =>
        Create(CelErrorCode.NoSuchField, "undefined field '" + field + "' in type '" + type + "'", place, field, actualType: type);

    // The same with the full name of the described type, which a literal of a well-known message names as it is written, not
    // as the CEL type that the name stands for.
    internal static CelError NoSuchField(string field, string typeName, CelSourceSpan place) =>
        Create(CelErrorCode.NoSuchField, "undefined field '" + field + "' in type '" + typeName + "'", place, field, actualType: CelType.ObjectNamed(typeName));

    // FieldNotAccessible (2007): a field that the description excluded from expressions, with the reason it gives.
    internal static CelError FieldNotAccessible(string field, string typeName, string reason, CelSourceSpan place) =>
        Create(
            CelErrorCode.FieldNotAccessible,
            "field '" + field + "' of type '" + typeName + "' is not available to expressions: " + reason,
            place,
            field,
            actualType: CelType.ObjectNamed(typeName));

    // InvalidComprehensionRange (2005): a range that is not a list, a map or dyn.
    internal static CelError InvalidComprehensionRange(CelType type, CelSourceSpan place) =>
        Create(
            CelErrorCode.InvalidComprehensionRange,
            "expression of type '" + type + "' cannot be the range of a comprehension (must be list, map, or dyn)",
            place,
            actualType: type);

    // NotAType (2006): the name of a message literal resolves to a declaration that is not an object type, at the literal.
    internal static CelError NotAType(string name, CelSourceSpan place) =>
        Create(CelErrorCode.NotAType, "'" + name + "' is not a type", place, name);

    // TypeNotConstructible (2008): a message literal of an object type whose description is not constructible, at the
    // literal.
    internal static CelError TypeNotConstructible(string type, CelSourceSpan place) =>
        Create(
            CelErrorCode.TypeNotConstructible,
            "type '" + type + "' cannot be created by an expression: object literals are supported only for protobuf messages",
            place,
            type);

    // InvalidFormatString (2010): the answer of the validator of a call of format; the function is the subject.
    internal static CelError InvalidFormatString(string detail, CelSourceSpan place) =>
        Create(CelErrorCode.InvalidFormatString, "invalid format string: " + detail, place, "format");

    // InvalidRegex (2009): the pattern of matches, a string literal, that the preparation refused. The text is complete: the preparer
    // of the library writes it, the same as the evaluation of a pattern from data. The place is the literal.
    internal static CelError InvalidRegex(string message, CelSourceSpan place) =>
        Create(CelErrorCode.InvalidRegex, message, place);

    // InsufficientStack (9000): the node that checking did not go into.
    internal static CelError InsufficientStack(CelSourceSpan place) =>
        Create(CelErrorCode.InsufficientStack, "insufficient execution stack to continue type checking", place);

    // InternalError (9001): an unexpected exception at the node being checked, kept in CelError.Exception.
    internal static CelError InternalError(Exception exception, CelSourceSpan place) =>
        new(
            CelErrorCode.InternalError,
            CelErrorStage.Check,
            "internal error during type checking; the original exception is in CelError.Exception",
            place.Start,
            null,
            exception,
            place.Length);

    // InternalError (9001) while the program is planned, after type checking: an unexpected exception at the node being planned,
    // kept in CelError.Exception. Planning belongs to the stage Check.
    internal static CelError InternalErrorWhilePlanning(Exception exception, CelSourceSpan place) =>
        new(
            CelErrorCode.InternalError,
            CelErrorStage.Check,
            "internal error during planning; the original exception is in CelError.Exception",
            place.Start,
            null,
            exception,
            place.Length);

    private static CelError Create(
        CelErrorCode code,
        string message,
        CelSourceSpan place,
        string? subject = null,
        CelType? expectedType = null,
        CelType? actualType = null) =>
        new(code, CelErrorStage.Check, message, place.Start, null, null, place.Length, subject, expectedType, actualType);
}

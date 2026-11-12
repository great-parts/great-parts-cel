using System;
using System.Globalization;
using System.Text;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Evaluation;

// The error values of evaluation, with the messages of the error code table. The position of each is the
// offset of a node, and the line and column are formed only when the error is read. Creating one allocates
// the error object only: the parts of its message are references to objects that already exist or a number,
// and the text is formed when it is read. The errors of the standard library functions come through their
// call sites.
internal static class EvaluationErrors
{
    private const string InsufficientStackText = "insufficient execution stack to continue evaluation";

    private static readonly Func<CelError, string> UnboundText =
        static error => "no value is bound to variable '" + (string)error.First! + "'";

    // The variable node gives the name and the declared type; the value is kept by ErrorValueParts.
    private static readonly Func<CelError, string> BindingTypeMismatchText = static error =>
    {
        var variable = (VariablePlanNode)error.First!;
        return "value of variable '" + variable.Name + "' has type '" + MismatchType(error, variable.DeclaredType!, variable.Types)
            + "', which does not match the declared type '" + variable.DeclaredType + "'";
    };

    // The message of the error without a place that the read gives a place.
    private static readonly Func<CelError, string> AtReadText = static error => ((CelError)error.First!).Message;

    private static readonly Func<CelError, string> CostLimitExceededText = static error =>
        "evaluation cost limit of " + ((ulong)error.Number).ToString(CultureInfo.InvariantCulture) + " exceeded";

    // The exception is the first part, the overload the second, and the byte tells whether the cost function threw.
    private static readonly Func<CelError, string> ApplicationExceptionText = static error =>
    {
        var overload = (OverloadBinding)error.Second!;
        string callee = "overload '" + overload.Declaration.OverloadId + "' of function '" + overload.Function + "'";
        return "application code threw " + error.First!.GetType() + " in " + (error.Small != 0 ? "the cost function of " + callee : callee);
    };

    // The overload gives the declared result type; the result is kept by ErrorValueParts.
    private static readonly Func<CelError, string> ImplementationResultMismatchText = static error =>
    {
        var overload = (OverloadBinding)error.First!;
        CelType resultType = overload.Declaration.ResultType;
        return "implementation of overload '" + overload.Declaration.OverloadId + "' of function '" + overload.Function
            + "' returned a value of type '" + MismatchType(error, resultType, null) + "', expected '" + resultType + "'";
    };

    // The exception is the first part, the description the second, the field number the number, and the byte tells
    // whether the presence test threw.
    private static readonly Func<CelError, string> FieldExceptionText = static error =>
    {
        var owner = (CelObjectType)error.Second!;
        string callee = "field '" + owner.Fields[(int)error.Number].Name + "' of type '" + owner.Name + "'";
        return "application code threw " + error.First!.GetType() + " in " + (error.Small != 0 ? "the presence test of " + callee : callee);
    };

    // The exception is the first part and the description the second.
    private static readonly Func<CelError, string> EqualityExceptionText = static error =>
        "application code threw " + error.First!.GetType() + " in the equality of objects of type '" + ((CelObjectType)error.Second!).Name + "'";

    private static readonly Func<CelError, string> CreationExceptionText = static error =>
        "application code threw " + error.First!.GetType() + " in the creation of an object of type '" + ((CelObjectType)error.Second!).Name + "'";

    // The field is the first part, the description of the object the second.
    private static readonly Func<CelError, string> NoSuchObjectFieldText =
        static error => "no such field '" + (string)error.First! + "' in " + ((CelObjectType)error.Second!).Name;

    private static readonly Func<CelError, string> NoSuchFieldText =
        static error => "no such field '" + (string)error.First! + "' in " + KindName(error);

    private static readonly Func<CelError, string> FieldSelectionNotSupportedText = static error =>
        "value of type '" + KindName(error) + "' does not support field selection of '" + (string)error.First! + "'";

    private static readonly Func<CelError, string> InvalidComprehensionRangeText = static error =>
        "value of type '" + KindName(error) + "' cannot be the range of a comprehension (must be list or map)";

    private static readonly Func<CelError, string> DuplicateMapKeyText = static error => "duplicate map key: " + KeyText(ErrorValueParts.ValueOf(error));

    private static readonly Func<CelError, string> UnsupportedMapKeyTypeText =
        static error => "unsupported map key type '" + KindName(error) + "'";

    private static readonly Func<CelError, string> InternalErrorText =
        static _ => "internal error during evaluation; the original exception is in CelError.Exception";

    // Unbound (3009): a variable without a value in the bindings, at its identifier.
    internal static CelValue Unbound(string name, LineTable lines, int offset) =>
        Create(CelErrorCode.Unbound, UnboundText, lines, offset, name);

    // BindingTypeMismatch (3011): a bound value that does not fit the declared type of the variable at some depth,
    // at the identifier. An object value is named by its description, or by the .NET type of an instance without one.
    internal static CelValue BindingTypeMismatch(VariablePlanNode variable, CelValue value, LineTable lines, int offset) =>
        CelValue.FromError(WithValue(CelErrorCode.BindingTypeMismatch, BindingTypeMismatchText, lines, offset, variable, value, variable.Types));

    // The result of a call whose arguments do not match the overload or the candidates: the error value of an element of
    // an argument, with the place of the call, or else EvalNoMatchingOverload.
    internal static CelValue NotMatched(TypeFit fit, string function, ReadOnlySpan<CelValue> values, Frame frame, int offset) =>
        fit.ElementError is { } elementError
            ? Placed(CelValue.FromError(elementError), frame.Lines, offset)
            : new CallSite(function, offset, frame.Lines, types: frame.Types).NoMatchingOverload(values);

    // An error of a bound value without a place, given the place of the read of its variable.
    internal static CelValue AtRead(CelError error, LineTable lines, int offset) =>
        Create(error.Code, AtReadText, lines, offset, error);

    // CostLimitExceeded (3013): the next operation would exceed the limit, at its node.
    internal static CelValue CostLimitExceeded(ulong limit, LineTable lines, int offset) =>
        CelValue.FromError(new CelError(CelErrorCode.CostLimitExceeded, CostLimitExceededText, lines, offset, number: (long)limit));

    // ApplicationException (3014): application code threw, at the call; the exception is kept.
    internal static CelValue ApplicationException(Exception exception, OverloadBinding overload, bool inCostFunction, LineTable lines, int offset) =>
        CelValue.FromError(new CelError(
            CelErrorCode.ApplicationException,
            ApplicationExceptionText,
            lines,
            offset,
            exception,
            overload,
            small: inCostFunction ? (byte)1 : (byte)0));

    // ImplementationResultMismatch (3028): a result of application code that does not fit the declared result type of
    // its overload, at the call.
    internal static CelValue ImplementationResultMismatch(OverloadBinding overload, CelValue result, TypeIndex types, LineTable lines, int offset) =>
        CelValue.FromError(WithValue(CelErrorCode.ImplementationResultMismatch, ImplementationResultMismatchText, lines, offset, overload, result, types));

    // ApplicationException (3014): application code threw in the read or the presence test of a field of an object, at
    // the node that read it; the exception is kept.
    internal static CelValue FieldException(Exception exception, CelObjectType owner, int number, bool presence, LineTable lines, int offset) =>
        CelValue.FromError(new CelError(
            CelErrorCode.ApplicationException,
            FieldExceptionText,
            lines,
            offset,
            exception,
            owner,
            number,
            presence ? (byte)1 : (byte)0));

    // ApplicationException (3014): application code threw in the equality that a description does itself, at the node that
    // compared; the exception is kept.
    internal static CelValue EqualityException(Exception exception, CelObjectType owner, LineTable lines, int offset) =>
        CelValue.FromError(new CelError(CelErrorCode.ApplicationException, EqualityExceptionText, lines, offset, exception, owner));

    // ApplicationException (3014): application code threw in the creation of an object by a message literal, at the literal;
    // the exception is kept.
    internal static CelValue CreationException(Exception exception, CelObjectType owner, LineTable lines, int offset) =>
        CelValue.FromError(new CelError(CelErrorCode.ApplicationException, CreationExceptionText, lines, offset, exception, owner));

    // EvalNoSuchField (3001): a field that the description of an object value does not have, with the full name of its
    // type, at the selection.
    internal static CelValue NoSuchField(string field, CelObjectType description, LineTable lines, int offset) =>
        CelValue.FromError(new CelError(CelErrorCode.EvalNoSuchField, NoSuchObjectFieldText, lines, offset, field, description));

    // A value read from application data, a field or a description: an error value without a place gets the place of the
    // node that read it; any other value is returned as it is.
    internal static CelValue Placed(CelValue value, LineTable lines, int offset) =>
        value.TryGetError(out CelError? error) && error.Location.Line == 0 ? AtRead(error, lines, offset) : value;

    // EvalNoSuchField (3001): a field that a map does not have, at the selection.
    internal static CelValue NoSuchField(string field, CelValueKind kind, LineTable lines, int offset) =>
        Create(CelErrorCode.EvalNoSuchField, NoSuchFieldText, lines, offset, field, kind);

    // FieldSelectionNotSupported (3025): a selection on a value without fields, at the selection.
    internal static CelValue FieldSelectionNotSupported(CelValueKind kind, string field, LineTable lines, int offset) =>
        Create(CelErrorCode.FieldSelectionNotSupported, FieldSelectionNotSupportedText, lines, offset, field, kind);

    // The same for the value of the operand: a value of an abstract type is named by its type, any other by its kind.
    internal static CelValue FieldSelectionNotSupported(CelValue operand, string field, LineTable lines, int offset) =>
        operand.Kind == CelValueKind.Abstract
            ? CelValue.FromError(new CelError(
                CelErrorCode.FieldSelectionNotSupported, FieldSelectionNotSupportedText, lines, offset, field, ObjectName(operand, null), small: (byte)operand.Kind))
            : FieldSelectionNotSupported(operand.Kind, field, lines, offset);

    // EvalInvalidComprehensionRange (3026): a range that is neither a list nor a map, at the range.
    internal static CelValue InvalidComprehensionRange(CelValue range, TypeIndex types, LineTable lines, int offset) =>
        CelValue.FromError(new CelError(
            CelErrorCode.EvalInvalidComprehensionRange, InvalidComprehensionRangeText, lines, offset, null, ObjectName(range, types), small: (byte)range.Kind));

    // DuplicateMapKey (3010): a key of a map literal equal to an earlier key, at the literal. The key is
    // written as a literal of its kind.
    internal static CelValue DuplicateMapKey(CelValue key, LineTable lines, int offset) =>
        CelValue.FromError(ErrorValueParts.Create(CelErrorCode.DuplicateMapKey, DuplicateMapKeyText, lines, offset, null, key));

    // UnsupportedMapKeyType (3027): a key of a map literal of a kind that cannot be a map key, at the key.
    internal static CelValue UnsupportedMapKeyType(CelValue key, TypeIndex types, LineTable lines, int offset) =>
        CelValue.FromError(new CelError(
            CelErrorCode.UnsupportedMapKeyType, UnsupportedMapKeyTypeText, lines, offset, null, ObjectName(key, types), small: (byte)key.Kind));

    // InsufficientStack (9000): the node that did not evaluate its children.
    internal static CelValue InsufficientStack(LineTable lines, int offset) =>
        Create(CelErrorCode.InsufficientStack, CelError.FirstPart, lines, offset, InsufficientStackText);

    // InternalError (9001): an unexpected exception of the library, at the recorded node; the exception is kept.
    internal static CelValue InternalError(Exception exception, LineTable lines, int offset) =>
        Create(CelErrorCode.InternalError, InternalErrorText, lines, offset, exception);

    // An error whose parts are the first and a kind.
    private static CelValue Create(CelErrorCode code, Func<CelError, string> format, LineTable lines, int offset, object? first, CelValueKind kind = default) =>
        CelValue.FromError(new CelError(code, format, lines, offset, first, small: (byte)kind));

    // The actual type of a value that does not fit the type, as ValueTypeRules.MismatchType names it: a list or a map
    // is descended again when the message is read, with the type index that describes the objects in it; a value of
    // another kind does not fit at the top level.
    private static string? MismatchType(CelError error, CelType type, TypeIndex? types) =>
        ErrorValueParts.KindOf(error) is CelValueKind.List or CelValueKind.Map
            ? ValueTypeRules.MismatchType(ErrorValueParts.ValueOf(error), type, false, types)
            : KindName(error);

    // An error whose message names a value: ErrorValueParts keeps it, and an object value is kept as the name of its
    // type, its description or the .NET type of an instance without one, resolved when the error is created.
    private static CelError WithValue(CelErrorCode code, Func<CelError, string> format, LineTable lines, int offset, object first, CelValue value, TypeIndex? types) =>
        value.Kind == CelValueKind.Object
            ? new CelError(code, format, lines, offset, first, ObjectName(value, types), small: (byte)CelValueKind.Object)
            : ErrorValueParts.Create(code, format, lines, offset, first, value);

    // The name of the type of an object value as an object that already exists: its description, or the .NET type of
    // an instance without one; null for a value of another kind, whose kind names it.
    private static object? ObjectName(CelValue value, TypeIndex? types)
    {
        if (value.TryGetAbstract(out AbstractValue? content))
        {
            return content.Type.Name;
        }

        if (value.Kind != CelValueKind.Object)
        {
            return null;
        }

        _ = value.TryGetObject(out object? instance);
        return (object?)ObjectRules.Describe(value, types) ?? instance!.GetType();
    }

    // The name of the type of the value of an error by its kind; for an object value, the name kept in the second part.
    private static string KindName(CelError error) => ErrorValueParts.KindOf(error) switch
    {
        CelValueKind.Object => error.Second is CelObjectType description ? description.Name : error.Second!.ToString()!,
        CelValueKind.Abstract => (string)error.Second!,
        CelValueKind kind => ValueTypeNames.Of(kind),
    };

    // A key of one of the kinds of map keys, written as a CEL literal: int, uint, bool or string, as the printer of the
    // syntax tree writes a constant.
    private static string KeyText(CelValue key)
    {
        var text = new StringBuilder();
        Printer.AppendConstant(text, key);
        return text.ToString();
    }
}

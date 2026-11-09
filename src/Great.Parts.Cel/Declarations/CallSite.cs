using System;
using System.Text;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Declarations;

// The call that an implementation serves: errors of the implementation get the position of the call
// in the expression, so the implementation itself knows nothing about positions, and the cost of a
// result known only from its content goes to the meter of the evaluation. The type index of the environment and the
// exception boundary of application code serve the implementations that read objects: type and equality.
internal readonly struct CallSite
{
    // The kinds of the arguments of NoMatchingOverload, 4 bits each, fit a long up to this count.
    private const int PackedKinds = 16;

    // The message of an error without a place that the call gives its place.
    private static readonly Func<CelError, string> AtCallText = static error => ((CelError)error.First!).Message;

    // The function name is the first part; the kinds are packed in the number with their count in the byte, or for
    // more than 16 arguments are an array of one byte per argument, the second part. An object argument is named by
    // its description or its .NET type, kept in an array of names as the second part.
    private static readonly Func<CelError, string> NoMatchingOverloadText = static error =>
    {
        var message = new StringBuilder("no such overload: ").Append((string)error.First!).Append('(');
        byte[]? kinds = error.Second as byte[];
        object?[]? objects = error.Second as object?[];
        int count = kinds?.Length ?? error.Small;
        for (int i = 0; i < count; i++)
        {
            if (i > 0)
            {
                message.Append(", ");
            }

            var kind = (CelValueKind)(kinds is null ? (error.Number >> (4 * i)) & 0xF : kinds[i]);
            object? name = objects?[i];
            message.Append(name is null ? ValueTypeNames.Of(kind) : name is CelObjectType description ? description.Name : name.ToString());
        }

        return message.Append(')').ToString();
    };

    private readonly int _offset;
    private readonly LineTable _lines;

    internal CallSite(
        string function,
        int offset,
        LineTable lines,
        IEvaluationMeter? meter = null,
        TypeIndex? types = null,
        IApplicationBoundary? boundary = null,
        CelTimeZoneDatabase? timeZones = null)
    {
        Function = function;
        _offset = offset;
        _lines = lines;
        Meter = meter;
        Types = types;
        Boundary = boundary;
        TimeZones = timeZones;
    }

    // The function name, for messages.
    internal string Function { get; }

    // The meter of an evaluation with a cost limit, for the value rules that charge pair by pair; null
    // without a limit.
    internal IEvaluationMeter? Meter { get; }

    // The type index of the environment, which describes object values; null outside an evaluation.
    internal TypeIndex? Types { get; }

    // The exception boundary and the event counter of the evaluation, for the reads of fields of objects; null outside
    // an evaluation.
    internal IApplicationBoundary? Boundary { get; }

    // The time zone database of the environment, which the receivers of time with a zone resolve names against; null when the
    // environment has none.
    internal CelTimeZoneDatabase? TimeZones { get; }

    // Whether the evaluation has a cost limit.
    internal bool HasLimit => Meter is not null;

    // Charges units before the part of the result they pay for; nothing without a limit.
    internal void Charge(ulong units) => Meter?.Charge(units);

    // An error value of the stage Evaluate at the position of the call with a message that already exists.
    internal CelValue Error(CelErrorCode code, string message) =>
        CelValue.FromError(new CelError(code, CelError.FirstPart, _lines, _offset, message));

    // An error value of the stage Evaluate at the position of the call, whose message the format forms from its parts
    // when it is read: a part of unbounded length, such as a key, is not written during evaluation.
    internal CelValue Error(CelErrorCode code, Func<CelError, string> format, object? first, object? second = null, long number = 0, byte small = 0) =>
        CelValue.FromError(new CelError(code, format, _lines, _offset, first, second, number, small));

    // An error value of the stage Evaluate at the position of the call whose message names the value
    // (ErrorValueParts).
    internal CelValue ErrorWithValue(CelErrorCode code, Func<CelError, string> format, object? first, CelValue value) =>
        CelValue.FromError(ErrorValueParts.Create(code, format, _lines, _offset, first, value));

    // A value that an operation read: an error value without a place gets the place of the call.
    internal CelValue Placed(CelValue value) =>
        value.TryGetError(out CelError? error) && error.Location.Line == 0
            ? CelValue.FromError(new CelError(error.Code, AtCallText, _lines, _offset, error))
            : value;

    // EvalNoMatchingOverload: no overload of the function takes the kinds of these argument values. An
    // error value among them is named error. The message is formed when the error is read.
    internal CelValue NoMatchingOverload(ReadOnlySpan<CelValue> arguments)
    {
        if (arguments.Length > PackedKinds)
        {
            // Only a function of the application has so many parameters; the array is bounded by the text of the program.
            byte[] kinds = new byte[arguments.Length];
            for (int i = 0; i < kinds.Length; i++)
            {
                kinds[i] = (byte)arguments[i].Kind;
            }

            return Error(CelErrorCode.EvalNoMatchingOverload, NoMatchingOverloadText, Function, kinds);
        }

        long packed = 0;
        object?[]? objects = null;
        for (int i = 0; i < arguments.Length; i++)
        {
            packed |= (long)arguments[i].Kind << (4 * i);
            if (arguments[i].TryGetObject(out object? instance))
            {
                // An object argument is named by its type, resolved now; only an argument of static type dyn gets here.
                objects ??= new object?[arguments.Length];
                objects[i] = (object?)ObjectRules.Describe(arguments[i], Types) ?? instance.GetType();
            }
        }

        return Error(CelErrorCode.EvalNoMatchingOverload, NoMatchingOverloadText, Function, objects, packed, (byte)arguments.Length);
    }
}

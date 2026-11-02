using System;
using System.Text;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Declarations;

// The call that an implementation serves: errors of the implementation get the position of the call
// in the expression, so the implementation itself knows nothing about positions.
internal readonly struct CallSite
{
    private readonly int _offset;
    private readonly LineTable _lines;

    internal CallSite(string function, int offset, LineTable lines)
    {
        Function = function;
        _offset = offset;
        _lines = lines;
    }

    // The function name, for messages.
    internal string Function { get; }

    // An error value of the stage Evaluate at the position of the call.
    internal CelValue Error(CelErrorCode code, string message) =>
        CelValue.FromError(new CelError(code, CelErrorStage.Evaluate, message, _lines.GetLocation(_offset), null, null));

    // EvalNoMatchingOverload: no overload of the function takes the kinds of these argument values.
    internal CelValue NoMatchingOverload(ReadOnlySpan<CelValue> arguments)
    {
        var message = new StringBuilder("no such overload: ").Append(Function).Append('(');
        for (int i = 0; i < arguments.Length; i++)
        {
            if (i > 0)
            {
                message.Append(", ");
            }

            message.Append(ValueTypeNames.Of(arguments[i].Kind));
        }

        return Error(CelErrorCode.EvalNoMatchingOverload, message.Append(')').ToString());
    }
}

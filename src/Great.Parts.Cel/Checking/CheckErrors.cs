using System;
using System.Text;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Checking;

// The check errors, with the messages of the error code table. Types are written as in the language
// definition, such as list(int).
internal static class CheckErrors
{
    // UndeclaredReference (2000): a name that is no variable, or a function without overloads. The name
    // resolution container is empty until it arrives with the complete type checking.
    internal static CelError UndeclaredReference(string name, CelSourceLocation location) =>
        Create(CelErrorCode.UndeclaredReference, "undeclared reference to '" + name + "' (in container '')", location);

    // NoMatchingOverload (2001): no overload of a known function accepts the static argument types.
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

    private static CelError Create(CelErrorCode code, string message, CelSourceLocation location) =>
        new(code, CelErrorStage.Check, message, location, null, null);
}

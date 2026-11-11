using System;

namespace Great.Parts.Cel.Declarations;

// The key of a macro of an extension set as the declaration checks see it: a function of the application with the
// name, the call style and the argument count of a macro of the environment would never run, since the parser expands
// the call. Declarations does not reference Syntax, so a set describes its macros in this form.
internal readonly record struct MacroForm(string Name, string? Namespace, bool IsReceiver, int ArgumentCount)
{
    // The argument count of a macro that takes any number of arguments, as in the macro table.
    internal const int AnyArgumentCount = -1;

    // Whether an overload of a function has the form of the macro: the same call style and, but for a macro of any
    // number, the same count of arguments, the receiver not counted. A macro with a namespace, such as math.greatest, is
    // written as a call on the identifier of the namespace, so the function that has its form is the global one with the
    // qualified name.
    internal bool Matches(string function, bool isReceiverStyle, int argumentCount)
    {
        if (ArgumentCount != AnyArgumentCount && ArgumentCount != argumentCount)
        {
            return false;
        }

        if (Namespace is null)
        {
            return IsReceiver == isReceiverStyle && string.Equals(function, Name, StringComparison.Ordinal);
        }

        return !isReceiverStyle
            && function.Length == Namespace.Length + 1 + Name.Length
            && function.StartsWith(Namespace, StringComparison.Ordinal)
            && function[Namespace.Length] == '.'
            && function.EndsWith(Name, StringComparison.Ordinal);
    }
}

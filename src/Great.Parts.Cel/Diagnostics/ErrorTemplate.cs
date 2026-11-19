using System;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Diagnostics;

// The text and the parts of an error of evaluation that keeps its message and parts as references to objects that already exist and
// forms them only when they are read: the text, and the name and the two types that the column "Parts" of the error table names
// for its code. A template is created once, in a static field, and shared by every error of its kind, so an error holds one
// reference to it and creating the error allocates nothing but the error. A part that the code does not have is null.
internal sealed class ErrorTemplate
{
    internal ErrorTemplate(
        Func<CelError, string> text,
        Func<CelError, string?>? subject = null,
        Func<CelError, CelType?>? expectedType = null,
        Func<CelError, CelType?>? actualType = null)
    {
        Text = text;
        Subject = subject;
        ExpectedType = expectedType;
        ActualType = actualType;
    }

    // The message, formed from the parts of the error.
    internal Func<CelError, string> Text { get; }

    // The name the error is about, or null for the error.
    internal Func<CelError, string?>? Subject { get; }

    // The expected CEL type, or null.
    internal Func<CelError, CelType?>? ExpectedType { get; }

    // The actual CEL type, or null.
    internal Func<CelError, CelType?>? ActualType { get; }
}

using System;

namespace Great.Parts.Cel.Eng.Difftest.Sides;

/// <summary>
/// A side that fails the run with exit code 2: the oracle sent a line or an answer that does not read, or a minimized case
/// agrees when it is run again. <see cref="Exception.Message"/> quotes no case and no line or answer of the oracle, so a run
/// with <c>--numbers-only</c> prints it; <see cref="QuotedMessage"/> adds the quote for the other runs
/// (docs/design/eng-difftest.md, "Findings and output").
/// </summary>
internal sealed class SideFailureException : Exception
{
    /// <summary>Creates the exception without a message.</summary>
    public SideFailureException()
    {
        QuotedMessage = Message;
    }

    /// <summary>Creates the exception with a message that quotes nothing.</summary>
    /// <param name="message">What failed, in one line.</param>
    public SideFailureException(string message)
        : base(message)
    {
        QuotedMessage = Message;
    }

    /// <summary>Creates the exception with a message that quotes nothing.</summary>
    /// <param name="message">What failed, in one line.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public SideFailureException(string message, Exception innerException)
        : base(message, innerException)
    {
        QuotedMessage = Message;
    }

    /// <summary>Creates the exception with a message and the same message with the quote.</summary>
    /// <param name="message">What failed, in one line, without the quote.</param>
    /// <param name="quotedMessage">What failed, with the case, the line or the answer it quotes.</param>
    public SideFailureException(string message, string quotedMessage)
        : base(message)
    {
        QuotedMessage = quotedMessage;
    }

    /// <summary>The message with the case, the line or the answer it quotes.</summary>
    public string QuotedMessage { get; }
}

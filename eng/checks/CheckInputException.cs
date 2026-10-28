using System;

namespace Great.Parts.Cel.Eng.Checks;

/// <summary>
/// An input of a command that does not exist or cannot be read, such as a revision range that git does not know. The application
/// writes the message to standard error and returns the exit code 2.
/// </summary>
internal sealed class CheckInputException : Exception
{
    /// <summary>Creates the exception without a message.</summary>
    public CheckInputException()
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What is wrong with the input, in one line.</param>
    public CheckInputException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception.</summary>
    /// <param name="message">What is wrong with the input, in one line.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public CheckInputException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

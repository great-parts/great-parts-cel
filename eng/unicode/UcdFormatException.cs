using System;

namespace Great.Parts.Cel.Eng.Unicode;

/// <summary>An input of the generator that does not have the form that the generator reads: <c>inputs.txt</c> or a file of the database.</summary>
internal sealed class UcdFormatException : Exception
{
    /// <summary>Creates the exception for a line of a file.</summary>
    /// <param name="file">The name of the file.</param>
    /// <param name="line">The number of the line, from 1; 0 for the file as a whole.</param>
    /// <param name="problem">What is wrong, in one line.</param>
    internal UcdFormatException(string file, int line, string problem)
        : base(line > 0 ? $"{file}({line}): {problem}" : $"{file}: {problem}")
    {
    }

    /// <summary>Creates the exception without a message.</summary>
    public UcdFormatException()
    {
    }

    /// <summary>Creates the exception with a message.</summary>
    /// <param name="message">The message.</param>
    public UcdFormatException(string message)
        : base(message)
    {
    }

    /// <summary>Creates the exception with a message and an inner exception.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public UcdFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

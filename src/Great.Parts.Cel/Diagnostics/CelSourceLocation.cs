using System;

namespace Great.Parts.Cel.Diagnostics;

/// <summary>
/// A position in a text: the offset from the start of the text, the line and the column.
/// </summary>
/// <remarks>
/// <para>
/// The offset and the column count UTF-16 code units, so the offset is an index into the
/// <see cref="string"/> that holds the text, and the column is what editors such as Visual Studio Code
/// show. The offset starts at 0, the line and the column start at 1. Lines are separated by
/// <c>"\n"</c>, <c>"\r\n"</c> and <c>"\r"</c>, as in the CEL language definition.
/// </para>
/// <para>
/// A position in the expression text has no <see cref="SourceName"/>. A position in a source
/// document, such as a file that contains the expression or an environment description in JSON,
/// names that document in <see cref="SourceName"/>.
/// </para>
/// <para>
/// <c>default(CelSourceLocation)</c>, whose <see cref="Line"/> is 0, means "no position": for example,
/// the <see cref="CelError.Location"/> of an error found while building an environment.
/// </para>
/// <para>The structure is immutable and can be shared between threads.</para>
/// </remarks>
public readonly struct CelSourceLocation : IEquatable<CelSourceLocation>
{
    /// <summary>
    /// Creates a position in a named source document, such as the file that contains an expression: for
    /// example, the position at which an expression starts in its document, given in the compilation
    /// options. Positions in the expression text are created by the library.
    /// </summary>
    /// <param name="sourceName">The name of the document, for example a file path; shown to people, not opened by the library.</param>
    /// <param name="offset">The offset from the start of the document in UTF-16 code units, 0 or more.</param>
    /// <param name="line">The line number, 1 or more.</param>
    /// <param name="column">The column number in UTF-16 code units, 1 or more.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sourceName"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="sourceName"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="offset"/> is negative, or <paramref name="line"/> or <paramref name="column"/> is less than 1.
    /// </exception>
    public CelSourceLocation(string sourceName, int offset, int line, int column)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// The name of the source document the position belongs to, or <see langword="null"/> for a position in the expression text.
    /// </summary>
    public string? SourceName => throw new NotImplementedException();

    /// <summary>
    /// The offset from the start of the text in UTF-16 code units, starting at 0.
    /// </summary>
    public int Offset => throw new NotImplementedException();

    /// <summary>
    /// The line number, starting at 1; 0 when the instance means "no position".
    /// </summary>
    public int Line => throw new NotImplementedException();

    /// <summary>
    /// The column number in UTF-16 code units from the start of the line, starting at 1; 0 when the instance means "no position".
    /// </summary>
    public int Column => throw new NotImplementedException();

    /// <summary>
    /// Compares two positions: they are equal when the source names are equal by ordinal comparison
    /// and the offsets, lines and columns are equal.
    /// </summary>
    /// <param name="other">The position to compare with.</param>
    /// <returns><see langword="true"/> when the positions are equal.</returns>
    public bool Equals(CelSourceLocation other)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Compares this position with an object, as <see cref="Equals(CelSourceLocation)"/> does when the object is a <see cref="CelSourceLocation"/>.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns><see langword="true"/> when <paramref name="obj"/> is an equal position.</returns>
    public override bool Equals(object? obj)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Returns a hash code consistent with <see cref="Equals(CelSourceLocation)"/>.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Tells whether two positions are equal, as <see cref="Equals(CelSourceLocation)"/> does.
    /// </summary>
    /// <param name="left">The first position.</param>
    /// <param name="right">The second position.</param>
    /// <returns><see langword="true"/> when the positions are equal.</returns>
    public static bool operator ==(CelSourceLocation left, CelSourceLocation right)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Tells whether two positions differ, as the negation of <see cref="Equals(CelSourceLocation)"/>.
    /// </summary>
    /// <param name="left">The first position.</param>
    /// <param name="right">The second position.</param>
    /// <returns><see langword="true"/> when the positions differ.</returns>
    public static bool operator !=(CelSourceLocation left, CelSourceLocation right)
    {
        throw new NotImplementedException();
    }
}

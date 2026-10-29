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
    private readonly string? _sourceName;
    private readonly int _offset;
    private readonly int _line;
    private readonly int _column;

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
        ArgumentException.ThrowIfNullOrEmpty(sourceName);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfLessThan(line, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(column, 1);
        _sourceName = sourceName;
        _offset = offset;
        _line = line;
        _column = column;
    }

    // A position in the expression text, without a source name; the library computes it from a line table.
    internal CelSourceLocation(int offset, int line, int column)
    {
        _offset = offset;
        _line = line;
        _column = column;
    }

    /// <summary>
    /// The name of the source document the position belongs to, or <see langword="null"/> for a position in the expression text.
    /// </summary>
    public string? SourceName => _sourceName;

    /// <summary>
    /// The offset from the start of the text in UTF-16 code units, starting at 0.
    /// </summary>
    public int Offset => _offset;

    /// <summary>
    /// The line number, starting at 1; 0 when the instance means "no position".
    /// </summary>
    public int Line => _line;

    /// <summary>
    /// The column number in UTF-16 code units from the start of the line, starting at 1; 0 when the instance means "no position".
    /// </summary>
    public int Column => _column;

    /// <summary>
    /// Compares two positions: they are equal when the source names are equal by ordinal comparison
    /// and the offsets, lines and columns are equal.
    /// </summary>
    /// <param name="other">The position to compare with.</param>
    /// <returns><see langword="true"/> when the positions are equal.</returns>
    public bool Equals(CelSourceLocation other) =>
        string.Equals(_sourceName, other._sourceName, StringComparison.Ordinal)
        && _offset == other._offset
        && _line == other._line
        && _column == other._column;

    /// <summary>
    /// Compares this position with an object, as <see cref="Equals(CelSourceLocation)"/> does when the object is a <see cref="CelSourceLocation"/>.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns><see langword="true"/> when <paramref name="obj"/> is an equal position.</returns>
    public override bool Equals(object? obj) => obj is CelSourceLocation other && Equals(other);

    /// <summary>
    /// Returns a hash code consistent with <see cref="Equals(CelSourceLocation)"/>.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() =>
        HashCode.Combine(_sourceName?.GetHashCode(StringComparison.Ordinal) ?? 0, _offset, _line, _column);

    /// <summary>
    /// Tells whether two positions are equal, as <see cref="Equals(CelSourceLocation)"/> does.
    /// </summary>
    /// <param name="left">The first position.</param>
    /// <param name="right">The second position.</param>
    /// <returns><see langword="true"/> when the positions are equal.</returns>
    public static bool operator ==(CelSourceLocation left, CelSourceLocation right) => left.Equals(right);

    /// <summary>
    /// Tells whether two positions differ, as the negation of <see cref="Equals(CelSourceLocation)"/>.
    /// </summary>
    /// <param name="left">The first position.</param>
    /// <param name="right">The second position.</param>
    /// <returns><see langword="true"/> when the positions differ.</returns>
    public static bool operator !=(CelSourceLocation left, CelSourceLocation right) => !left.Equals(right);
}

using System;

namespace Great.Parts.Cel.Diagnostics;

/// <summary>
/// A fragment of a text: the position where it starts and its length, for example the part of an expression that an error
/// refers to.
/// </summary>
/// <remarks>
/// <para>
/// The length counts UTF-16 code units, as <see cref="string.Length"/> counts them, like <see cref="CelSourceLocation.Offset"/>
/// and <see cref="CelSourceLocation.Column"/>: the fragment of an expression <c>text</c> is
/// <c>text.Substring(span.Start.Offset, span.Length)</c>. The fragment of an error runs from its place to the end of the
/// node of the syntax tree that produced it, such as the identifier <c>totl</c> of <c>totl &gt; 100</c> or a call from
/// <c>(</c> to <c>)</c>.
/// </para>
/// <para>
/// <c>default(CelSourceSpan)</c>, whose <see cref="Start"/> has the line 0 and whose <see cref="Length"/> is 0, means "no
/// fragment": for example, the span of an error found while building an environment.
/// </para>
/// <para>The structure is immutable and can be shared between threads.</para>
/// </remarks>
public readonly struct CelSourceSpan : IEquatable<CelSourceSpan>
{
    // A span in the expression text; the library computes it from the node that gave an error.
    internal CelSourceSpan(CelSourceLocation start, int length)
    {
        Start = start;
        Length = length;
    }

    /// <summary>
    /// The position of the first code unit of the fragment.
    /// </summary>
    public CelSourceLocation Start { get; }

    /// <summary>
    /// The length of the fragment in UTF-16 code units, 0 or more; 0 for a fragment of no text, such as the end of the
    /// expression where a parse error expected more.
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Compares two fragments: they are equal when their starts are equal by
    /// <see cref="CelSourceLocation.Equals(CelSourceLocation)"/> and their lengths are equal.
    /// </summary>
    /// <param name="other">The fragment to compare with.</param>
    /// <returns><see langword="true"/> when the fragments are equal.</returns>
    public bool Equals(CelSourceSpan other) => Start.Equals(other.Start) && Length == other.Length;

    /// <summary>
    /// Compares this fragment with an object, as <see cref="Equals(CelSourceSpan)"/> does when the object is a
    /// <see cref="CelSourceSpan"/>.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns><see langword="true"/> when <paramref name="obj"/> is an equal fragment.</returns>
    public override bool Equals(object? obj) => obj is CelSourceSpan other && Equals(other);

    /// <summary>
    /// Returns a hash code consistent with <see cref="Equals(CelSourceSpan)"/>.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode() => HashCode.Combine(Start, Length);

    /// <summary>
    /// Tells whether two fragments are equal, as <see cref="Equals(CelSourceSpan)"/> does.
    /// </summary>
    /// <param name="left">The first fragment.</param>
    /// <param name="right">The second fragment.</param>
    /// <returns><see langword="true"/> when the fragments are equal.</returns>
    public static bool operator ==(CelSourceSpan left, CelSourceSpan right) => left.Equals(right);

    /// <summary>
    /// Tells whether two fragments differ, as the negation of <see cref="Equals(CelSourceSpan)"/>.
    /// </summary>
    /// <param name="left">The first fragment.</param>
    /// <param name="right">The second fragment.</param>
    /// <returns><see langword="true"/> when the fragments differ.</returns>
    public static bool operator !=(CelSourceSpan left, CelSourceSpan right) => !left.Equals(right);
}

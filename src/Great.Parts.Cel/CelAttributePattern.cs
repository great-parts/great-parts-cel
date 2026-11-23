using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel;

/// <summary>
/// A pattern of unknown data for partial evaluation: a variable and an optional path of qualifiers under it — field names,
/// list indexes and map keys — where a qualifier may match any, as the attribute patterns of cel-go.
/// </summary>
/// <remarks>
/// <para>
/// <c>new CelAttributePattern("request").Qualify("auth").QualifyAny()</c> marks unknown every value under
/// <c>request.auth</c> by any field or key. An attribute of an expression — a variable with a chain of selections
/// <c>.f</c> and <c>.?f</c>, indexes <c>[k]</c> and <c>[?k]</c> and presence tests <c>has(e.f)</c> — matches the pattern when the
/// variable has the same full name, as the bindings name it after the resolution of names in the container, and the
/// qualifiers agree as far as both go: with the pattern <c>request.auth</c>, the accesses <c>request.auth.claims</c>,
/// <c>request</c> and <c>size(request)</c> are unknown, and <c>request.time</c> is known. A string qualifier matches a field
/// and a string key; numbers of the kinds <c>int</c>, <c>uint</c> and <c>double</c> match by numeric equality; a key that is
/// an expression is evaluated first. Variables of comprehensions and of <c>cel.bind</c> never match.
/// </para>
/// <para>
/// The same type describes an unknown attribute in a result, <see cref="CelUnknown.Attribute"/>: the variable and the
/// qualifiers of the access up to the length of the pattern, with no qualifier that matches any. The pattern is immutable;
/// each <c>Qualify</c> member returns a new one.
/// </para>
/// </remarks>
public sealed class CelAttributePattern
{
    /// <summary>
    /// Creates a pattern of a whole variable.
    /// </summary>
    /// <param name="variable">The full name of the variable, as the bindings name it: <c>acme.limit</c> for <c>limit</c> in the container <c>acme</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="variable"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="variable"/> is empty.</exception>
    public CelAttributePattern(string variable)
    {
        ArgumentException.ThrowIfNullOrEmpty(variable);
        Variable = variable;
        Qualifiers = [];
    }

    private CelAttributePattern(string variable, ImmutableArray<CelValue?> qualifiers)
    {
        Variable = variable;
        Qualifiers = qualifiers;
    }

    /// <summary>
    /// The full name of the variable.
    /// </summary>
    public string Variable { get; }

    /// <summary>
    /// The qualifiers under the variable, in order: values of the kinds <c>string</c>, <c>int</c>, <c>uint</c> and <c>bool</c>,
    /// and <see langword="null"/> for a qualifier that matches any field, index or key.
    /// </summary>
    public ImmutableArray<CelValue?> Qualifiers { get; }

    /// <summary>
    /// Returns the pattern with one more qualifier: a field name or a string map key.
    /// </summary>
    /// <param name="fieldOrKey">The field name or key.</param>
    /// <returns>A new pattern.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fieldOrKey"/> is <see langword="null"/>.</exception>
    public CelAttributePattern Qualify(string fieldOrKey)
    {
        ArgumentNullException.ThrowIfNull(fieldOrKey);
        return With(CelValue.FromString(fieldOrKey));
    }

    /// <summary>
    /// Returns the pattern with one more qualifier: a list index or a map key of the kind <c>int</c>, which also matches equal
    /// <c>uint</c> and <c>double</c> keys.
    /// </summary>
    /// <param name="key">The index or key.</param>
    /// <returns>A new pattern.</returns>
    public CelAttributePattern Qualify(long key) => With(CelValue.FromInt64(key));

    /// <summary>
    /// Returns the pattern with one more qualifier: a map key of the kind <c>uint</c>, which also matches equal <c>int</c> and
    /// <c>double</c> keys and list indexes.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>A new pattern.</returns>
    public CelAttributePattern Qualify(ulong key) => With(CelValue.FromUInt64(key));

    /// <summary>
    /// Returns the pattern with one more qualifier: a map key of the kind <c>bool</c>.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>A new pattern.</returns>
    public CelAttributePattern Qualify(bool key) => With(CelValue.FromBoolean(key));

    /// <summary>
    /// Returns the pattern with one more qualifier that matches any field, list index or map key.
    /// </summary>
    /// <returns>A new pattern.</returns>
    public CelAttributePattern QualifyAny() => With(null);

    /// <summary>
    /// Returns the pattern as text for people, for example <c>request.auth.claims["x-id"][*]</c>: a field name that is an
    /// identifier after a dot, other strings quoted in brackets, numbers and <c>bool</c> in brackets with <c>u</c> after a
    /// <c>uint</c>, and <c>[*]</c> for a qualifier that matches any. The text is not part of the contract.
    /// </summary>
    /// <returns>The text of the pattern.</returns>
    public override string ToString()
    {
        var text = new StringBuilder(Variable);
        foreach (CelValue? qualifier in Qualifiers)
        {
            if (qualifier is not { } value)
            {
                text.Append("[*]");
            }
            else if (value.TryGetString(out string? name) && name is not null)
            {
                if (CelSyntax.IsIdentifier(name))
                {
                    text.Append('.').Append(name);
                }
                else
                {
                    text.Append('[').Append(CelSyntax.QuoteString(name)).Append(']');
                }
            }
            else if (value.TryGetInt64(out long index))
            {
                text.Append('[').Append(index.ToString(CultureInfo.InvariantCulture)).Append(']');
            }
            else if (value.TryGetUInt64(out ulong key))
            {
                text.Append('[').Append(key.ToString(CultureInfo.InvariantCulture)).Append("u]");
            }
            else if (value.TryGetBoolean(out bool flag))
            {
                text.Append(flag ? "[true]" : "[false]");
            }
        }

        return text.ToString();
    }

    private CelAttributePattern With(CelValue? qualifier) => new(Variable, Qualifiers.Add(qualifier));

    // The trail of an unknown attribute that an evaluation found: the variable and the qualifiers of the access.
    internal static CelAttributePattern OfTrail(string variable, ImmutableArray<CelValue?> qualifiers) => new(variable, qualifiers);
}

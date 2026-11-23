using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Great.Parts.Cel.Eng.Difftest.Sides;

/// <summary>The kind of a qualifier of an attribute: the kinds of a field, an index or a map key, and the one that matches any.</summary>
internal enum QualifierKind
{
    /// <summary>A field name or a <c>string</c> map key.</summary>
    String,

    /// <summary>An <c>int</c> index or key.</summary>
    Int,

    /// <summary>A <c>uint</c> key.</summary>
    Uint,

    /// <summary>A <c>bool</c> key.</summary>
    Bool,

    /// <summary>A pattern qualifier that matches any field, index or key; a trail has none.</summary>
    Any,
}

/// <summary>One qualifier of an attribute.</summary>
/// <param name="Kind">The kind.</param>
/// <param name="Text">The value as text: the name, the decimal number, <c>true</c> or <c>false</c>; empty for <see cref="QualifierKind.Any"/>.</param>
internal readonly record struct AttributeQualifier(QualifierKind Kind, string Text)
{
    /// <summary>The name of the kind in the protocol of the oracle: <c>string</c>, <c>int</c>, <c>uint</c>, <c>bool</c>, <c>any</c>.</summary>
    internal string KindName => Kind switch
    {
        QualifierKind.String => "string",
        QualifierKind.Int => "int",
        QualifierKind.Uint => "uint",
        QualifierKind.Bool => "bool",
        _ => "any",
    };

    /// <summary>Reads the name of a kind in the protocol of the oracle.</summary>
    /// <param name="name">The name.</param>
    /// <param name="kind">The kind.</param>
    /// <returns><see langword="true"/> when the protocol has the name.</returns>
    internal static bool TryParseKind(string name, out QualifierKind kind)
    {
        (bool known, kind) = name switch
        {
            "string" => (true, QualifierKind.String),
            "int" => (true, QualifierKind.Int),
            "uint" => (true, QualifierKind.Uint),
            "bool" => (true, QualifierKind.Bool),
            "any" => (true, QualifierKind.Any),
            _ => (false, QualifierKind.String),
        };
        return known;
    }
}

/// <summary>
/// An attribute of partial evaluation (docs/design/eng-difftest.md, "Partial evaluation"): a variable and the qualifiers under it.
/// As a pattern it names unknown data, with <see cref="QualifierKind.Any"/> for a qualifier that matches any; as a trail it is
/// one unknown part of a result. The comparison of two sides uses <see cref="Key"/>, which is the same for equal attributes.
/// </summary>
internal sealed class UnknownAttribute
{
    /// <summary>Creates an attribute.</summary>
    /// <param name="variable">The full name of the variable.</param>
    /// <param name="qualifiers">The qualifiers under it, in order.</param>
    internal UnknownAttribute(string variable, IReadOnlyList<AttributeQualifier> qualifiers)
    {
        Variable = variable;
        Qualifiers = qualifiers;
        Key = JsonSerializer.Serialize(qualifiers.Select(qualifier => qualifier.KindName + ":" + qualifier.Text).Prepend(variable).ToArray());
    }

    /// <summary>The full name of the variable.</summary>
    internal string Variable { get; }

    /// <summary>The qualifiers under the variable, in order.</summary>
    internal IReadOnlyList<AttributeQualifier> Qualifiers { get; }

    /// <summary>A text that is equal for equal attributes and differs for different ones: a JSON array of the variable and the typed qualifiers.</summary>
    internal string Key { get; }

    /// <summary>
    /// The attribute as text for people, as cel-go writes a trail: <c>x1.a["k z"][1][2u][true]</c>, with <c>[*]</c> for a qualifier
    /// that matches any.
    /// </summary>
    public override string ToString()
    {
        StringBuilder text = new(Variable);
        foreach (AttributeQualifier qualifier in Qualifiers)
        {
            switch (qualifier.Kind)
            {
                case QualifierKind.String when IsIdentifier(qualifier.Text):
                    text.Append('.').Append(qualifier.Text);
                    break;
                case QualifierKind.String:
                    text.Append('[').Append(JsonSerializer.Serialize(qualifier.Text)).Append(']');
                    break;
                case QualifierKind.Uint:
                    text.Append(CultureInfo.InvariantCulture, $"[{qualifier.Text}u]");
                    break;
                case QualifierKind.Any:
                    text.Append("[*]");
                    break;
                default:
                    text.Append(CultureInfo.InvariantCulture, $"[{qualifier.Text}]");
                    break;
            }
        }

        return text.ToString();
    }

    private static bool IsIdentifier(string name) =>
        name.Length > 0 && name.All(character => char.IsLetterOrDigit(character) || character == '_');
}

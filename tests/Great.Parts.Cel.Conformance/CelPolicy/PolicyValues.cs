using System;
using System.Globalization;
using System.IO;
using Cel.Expr;
using Great.Parts.Cel.Policy.Yaml;
using NullValue = Google.Protobuf.WellKnownTypes.NullValue;
using Value = Cel.Expr.Value;

namespace Great.Parts.Cel.Conformance.CelPolicy;

/// <summary>
/// Turns the YAML literal of an input or of an expected output into a value of a case, as cel-go reads it into a Go value and gives it
/// to the type adapter: <c>null</c>, a boolean, an integer, a floating point number and a string by the tag of the scalar, a sequence
/// as a list and a mapping as a map with string keys.
/// </summary>
internal static class PolicyValues
{
    /// <summary>The value of a node.</summary>
    /// <param name="node">The node.</param>
    /// <exception cref="InvalidDataException">The literal has a form the runner does not read: a key that is no scalar, or a number that does not parse.</exception>
    internal static Value ToValue(YamlNode node)
    {
        switch (node)
        {
            case YamlScalar scalar:
                return Scalar(scalar);
            case YamlSequence sequence:
                ListValue list = new();
                foreach (YamlNode item in sequence.Items)
                {
                    list.Values.Add(ToValue(item));
                }

                return new Value { ListValue = list };
            case YamlMapping mapping:
                MapValue map = new();
                foreach (YamlPair pair in mapping.Pairs)
                {
                    if (pair.Key is not YamlScalar key)
                    {
                        throw new InvalidDataException("a key of a literal is no scalar");
                    }

                    map.Entries.Add(new MapValue.Types.Entry { Key = new Value { StringValue = key.Value }, Value = ToValue(pair.Value) });
                }

                return new Value { MapValue = map };
            default:
                throw new InvalidDataException("a literal has an unknown kind of node");
        }
    }

    private static Value Scalar(YamlScalar scalar) => scalar.Tag switch
    {
        YamlNode.NullTag => new Value { NullValue = NullValue.NullValue },
        YamlNode.BoolTag => new Value { BoolValue = scalar.Value.Length > 0 && char.ToLowerInvariant(scalar.Value[0]) == 't' },
        YamlNode.IntTag => new Value { Int64Value = Integer(scalar.Value) },
        YamlNode.FloatTag => new Value { DoubleValue = Number(scalar.Value) },
        _ => new Value { StringValue = scalar.Value },
    };

    private static long Integer(string text)
    {
        string digits = text.Replace("_", string.Empty, StringComparison.Ordinal);
        return long.TryParse(digits, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long value)
            ? value
            : throw new InvalidDataException($"the integer '{text}' is not a decimal number the runner reads");
    }

    private static double Number(string text) =>
        double.TryParse(text.Replace("_", string.Empty, StringComparison.Ordinal), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            ? value
            : throw new InvalidDataException($"the number '{text}' is not one the runner reads");
}

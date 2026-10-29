using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Cel.Expr;
using Great.Parts.Cel.Eng.Difftest.Sides;

namespace Great.Parts.Cel.Eng.Difftest.Compare;

/// <summary>The CEL type names of values for signatures, and values and outcomes as one line of text for finding messages.</summary>
internal static class ValueText
{
    /// <summary>
    /// An outcome in a finding message, one line: <c>int 42</c>, <c>error 'division by zero'</c>,
    /// <c>check error 'found no matching overload'</c>. An error message is cut at its first line break.
    /// </summary>
    /// <param name="outcome">The outcome.</param>
    internal static string Describe(this SideOutcome outcome)
    {
        return outcome.Class switch
        {
            OutcomeClass.Value => TypeName(outcome.Result!) + " " + Format(outcome.Result!),
            OutcomeClass.EvaluationError => "error '" + FirstLine(outcome.Message) + "'",
            _ => StageName(outcome.Stage) + " error '" + FirstLine(outcome.Message) + "'",
        };
    }

    /// <summary>
    /// The CEL type name of a value as <c>type()</c> gives it, without parameters: <c>int</c>, <c>uint</c>, <c>double</c>,
    /// <c>bool</c>, <c>string</c>, <c>bytes</c>, <c>null_type</c>, <c>list</c>, <c>map</c>, <c>type</c>, or the name of an enum
    /// or a message.
    /// </summary>
    /// <param name="value">The value.</param>
    internal static string TypeName(Value value)
    {
        return value.KindCase switch
        {
            Value.KindOneofCase.NullValue => "null_type",
            Value.KindOneofCase.BoolValue => "bool",
            Value.KindOneofCase.Int64Value => "int",
            Value.KindOneofCase.Uint64Value => "uint",
            Value.KindOneofCase.DoubleValue => "double",
            Value.KindOneofCase.StringValue => "string",
            Value.KindOneofCase.BytesValue => "bytes",
            Value.KindOneofCase.ListValue => "list",
            Value.KindOneofCase.MapValue => "map",
            Value.KindOneofCase.TypeValue => "type",
            Value.KindOneofCase.EnumValue => value.EnumValue.Type,
            Value.KindOneofCase.ObjectValue => value.ObjectValue.TypeUrl[(value.ObjectValue.TypeUrl.LastIndexOf('/') + 1)..],
            _ => "unknown",
        };
    }

    /// <summary>
    /// A value as one line of CEL-like text: <c>-1</c>, <c>1u</c>, <c>1.5</c>, <c>"a\n"</c>, <c>b"\x00"</c>, <c>[1, 2]</c>,
    /// <c>{"a": 1}</c>. Strings and bytes escape every character that is not printable ASCII.
    /// </summary>
    /// <param name="value">The value.</param>
    internal static string Format(Value value)
    {
        return value.KindCase switch
        {
            Value.KindOneofCase.NullValue => "null",
            Value.KindOneofCase.BoolValue => value.BoolValue ? "true" : "false",
            Value.KindOneofCase.Int64Value => value.Int64Value.ToString(CultureInfo.InvariantCulture),
            Value.KindOneofCase.Uint64Value => value.Uint64Value.ToString(CultureInfo.InvariantCulture) + "u",
            Value.KindOneofCase.DoubleValue => DoubleText(value.DoubleValue),
            Value.KindOneofCase.StringValue => Quoted(string.Empty, value.StringValue.EnumerateRunes().Select(rune => rune.Value)),
            Value.KindOneofCase.BytesValue => Quoted("b", value.BytesValue.Select(unit => (int)unit)),
            Value.KindOneofCase.ListValue => "[" + string.Join(", ", value.ListValue.Values.Select(Format)) + "]",
            Value.KindOneofCase.MapValue => "{" + string.Join(", ", value.MapValue.Entries.Select(entry => Format(entry.Key) + ": " + Format(entry.Value))) + "}",
            Value.KindOneofCase.TypeValue => value.TypeValue,
            Value.KindOneofCase.EnumValue => value.EnumValue.Type + "(" + value.EnumValue.Value.ToString(CultureInfo.InvariantCulture) + ")",
            Value.KindOneofCase.ObjectValue => value.ObjectValue.TypeUrl + "{…}",
            _ => "unknown",
        };
    }

    private static string StageName(CompileStage stage) => stage switch
    {
        CompileStage.Parse => "parse",
        CompileStage.Check => "check",
        _ => "program",
    };

    private static string FirstLine(string message)
    {
        int end = message.AsSpan().IndexOfAny('\r', '\n');
        return end < 0 ? message : message[..end];
    }

    private static string DoubleText(double value)
    {
        if (double.IsNaN(value))
        {
            return "NaN";
        }

        if (double.IsInfinity(value))
        {
            return value > 0 ? "+Infinity" : "-Infinity";
        }

        string text = value.ToString("R", CultureInfo.InvariantCulture);
        return text.AsSpan().ContainsAny('.', 'E') ? text : text + ".0";
    }

    // Printable ASCII stays itself except the quote and the backslash; other units are \xhh up to 0xFF, then \uhhhh or \Uhhhhhhhh.
    private static string Quoted(string prefix, IEnumerable<int> units)
    {
        StringBuilder text = new(prefix);
        text.Append('"');
        foreach (int unit in units)
        {
            if (unit is '"' or '\\')
            {
                text.Append('\\').Append((char)unit);
            }
            else if (unit is >= 0x20 and < 0x7F)
            {
                text.Append((char)unit);
            }
            else
            {
                string format = unit switch
                {
                    <= 0xFF => "\\x{0:x2}",
                    <= 0xFFFF => "\\u{0:x4}",
                    _ => "\\U{0:x8}",
                };
                text.AppendFormat(CultureInfo.InvariantCulture, format, unit);
            }
        }

        return text.Append('"').ToString();
    }
}

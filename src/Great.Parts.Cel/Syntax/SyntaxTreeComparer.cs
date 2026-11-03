using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Syntax;

/// <summary>
/// Compares two syntax trees for the property "parse, print, parse gives the same tree".
/// </summary>
/// <remarks>
/// Equal trees have the same node classes, children in the same order, names, functions, fields, flags — the
/// presence test, the optional indices and entries — and comprehension variables, and constants of the same kind and
/// value, doubles by their bits. Ids, positions and macro calls are not compared. The comparison recurses without
/// checking the stack: it serves trees within the limits of parsing.
/// </remarks>
internal static class SyntaxTreeComparer
{
    /// <summary>
    /// Finds the first difference of two trees.
    /// </summary>
    /// <param name="left">The root of the first tree.</param>
    /// <param name="right">The root of the second tree.</param>
    /// <returns>
    /// <see langword="null"/> for equal trees; otherwise the path from the roots to the first difference with both
    /// values, such as <c>root.arguments[1].function: "_+_" vs "_-_"</c>.
    /// </returns>
    internal static string? Difference(ExprNode left, ExprNode right)
    {
        return Node(left, right) is { } difference ? "root" + difference : null;
    }

    // The difference of two nodes as a path relative to them, such as ".function: ...", or null.
    private static string? Node(ExprNode left, ExprNode right)
    {
        if (left.GetType() != right.GetType())
        {
            return Values(string.Empty, left.GetType().Name, right.GetType().Name);
        }

        return (left, right) switch
        {
            (ConstantNode a, ConstantNode b) => Constant(a.Value, b.Value),
            (IdentNode a, IdentNode b) => Text(".name", a.Name, b.Name),
            (SelectNode a, SelectNode b) => Select(a, b),
            (CallNode a, CallNode b) => Call(a, b),
            (ListNode a, ListNode b) => List(a, b),
            (StructNode a, StructNode b) => Struct(a, b),
            _ => Comprehension((ComprehensionNode)left, (ComprehensionNode)right),
        };
    }

    private static string? Select(SelectNode left, SelectNode right) =>
        Text(".field", left.Field, right.Field)
            ?? Flag(".isPresenceTest", left.IsPresenceTest, right.IsPresenceTest)
            ?? Child(".operand", left.Operand, right.Operand);

    private static string? Call(CallNode left, CallNode right)
    {
        if (Text(".function", left.Function, right.Function) is { } function)
        {
            return function;
        }

        if (left.Target is null != right.Target is null)
        {
            return Values(".target", left.Target is null ? "null" : "a target", right.Target is null ? "null" : "a target");
        }

        return (left.Target is null ? null : Child(".target", left.Target, right.Target!))
            ?? Nodes(".arguments", left.Arguments, right.Arguments);
    }

    private static string? List(ListNode left, ListNode right)
    {
        if (!left.OptionalIndices.AsSpan().SequenceEqual(right.OptionalIndices.AsSpan()))
        {
            return Values(".optionalIndices", Indices(left.OptionalIndices), Indices(right.OptionalIndices));
        }

        return Nodes(".elements", left.Elements, right.Elements);
    }

    private static string? Struct(StructNode left, StructNode right)
    {
        if (Text(".messageName", left.MessageName, right.MessageName) is { } name)
        {
            return name;
        }

        if (Count(".entries", left.Entries.Length, right.Entries.Length) is { } count)
        {
            return count;
        }

        for (int i = 0; i < left.Entries.Length; i++)
        {
            if (Entry(left.Entries[i], right.Entries[i]) is { } difference)
            {
                return ".entries[" + i.ToString(CultureInfo.InvariantCulture) + "]" + difference;
            }
        }

        return null;
    }

    private static string? Entry(StructEntry left, StructEntry right)
    {
        if (Flag(".isOptional", left.IsOptional, right.IsOptional) is { } optional)
        {
            return optional;
        }

        if (left.FieldName is not null || right.FieldName is not null)
        {
            return Text(".fieldName", left.FieldName ?? "null", right.FieldName ?? "null")
                ?? Child(".value", left.Value, right.Value);
        }

        return Child(".key", left.Key, right.Key) ?? Child(".value", left.Value, right.Value);
    }

    private static string? Comprehension(ComprehensionNode left, ComprehensionNode right) =>
        Text(".iterVar", left.IterVar, right.IterVar)
            ?? Text(".accuVar", left.AccuVar, right.AccuVar)
            ?? Child(".iterRange", left.IterRange, right.IterRange)
            ?? Child(".accuInit", left.AccuInit, right.AccuInit)
            ?? Child(".loopCondition", left.LoopCondition, right.LoopCondition)
            ?? Child(".loopStep", left.LoopStep, right.LoopStep)
            ?? Child(".result", left.Result, right.Result);

    private static string? Constant(CelValue left, CelValue right)
    {
        if (left.Kind != right.Kind)
        {
            return Values(".kind", left.Kind.ToString(), right.Kind.ToString());
        }

        return SameConstant(left, right) ? null : Values(".value", Literal(left), Literal(right));
    }

    private static bool SameConstant(CelValue left, CelValue right) => left.Kind switch
    {
        CelValueKind.Int => AsInt64(left) == AsInt64(right),
        CelValueKind.UInt => AsUInt64(left) == AsUInt64(right),
        CelValueKind.Double => BitConverter.DoubleToInt64Bits(AsDouble(left)) == BitConverter.DoubleToInt64Bits(AsDouble(right)),
        CelValueKind.String => string.Equals(AsString(left), AsString(right), StringComparison.Ordinal),
        CelValueKind.Bytes => AsBytes(left).Span.SequenceEqual(AsBytes(right).Span),
        CelValueKind.Bool => AsBoolean(left) == AsBoolean(right),
        _ => true,
    };

    private static long AsInt64(CelValue value) => value.TryGetInt64(out long result) ? result : 0;

    private static ulong AsUInt64(CelValue value) => value.TryGetUInt64(out ulong result) ? result : 0;

    private static double AsDouble(CelValue value) => value.TryGetDouble(out double result) ? result : 0;

    private static string? AsString(CelValue value) => value.TryGetString(out string? result) ? result : null;

    private static ReadOnlyMemory<byte> AsBytes(CelValue value) => value.TryGetBytes(out ReadOnlyMemory<byte> result) ? result : default;

    private static bool AsBoolean(CelValue value) => value.TryGetBoolean(out bool result) && result;

    private static string? Nodes(string path, ImmutableArray<ExprNode> left, ImmutableArray<ExprNode> right)
    {
        if (Count(path, left.Length, right.Length) is { } count)
        {
            return count;
        }

        for (int i = 0; i < left.Length; i++)
        {
            if (Node(left[i], right[i]) is { } difference)
            {
                return path + "[" + i.ToString(CultureInfo.InvariantCulture) + "]" + difference;
            }
        }

        return null;
    }

    private static string? Child(string path, ExprNode left, ExprNode right) =>
        Node(left, right) is { } difference ? path + difference : null;

    private static string? Count(string path, int left, int right) =>
        left == right
            ? null
            : Values(path, left.ToString(CultureInfo.InvariantCulture) + " items", right.ToString(CultureInfo.InvariantCulture) + " items");

    private static string? Text(string path, string left, string right) =>
        string.Equals(left, right, StringComparison.Ordinal) ? null : Values(path, "\"" + left + "\"", "\"" + right + "\"");

    private static string? Flag(string path, bool left, bool right) =>
        left == right ? null : Values(path, left ? "true" : "false", right ? "true" : "false");

    private static string Values(string path, string left, string right) => path + ": " + left + " vs " + right;

    private static string Indices(ImmutableArray<int> indices)
    {
        var text = new StringBuilder("[");
        for (int i = 0; i < indices.Length; i++)
        {
            text.Append(i > 0 ? ", " : string.Empty).Append(indices[i].ToString(CultureInfo.InvariantCulture));
        }

        return text.Append(']').ToString();
    }

    private static string Literal(CelValue value)
    {
        var text = new StringBuilder();
        Printer.AppendConstant(text, value);
        return text.ToString();
    }
}

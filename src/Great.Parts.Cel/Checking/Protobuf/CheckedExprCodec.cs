using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Great.Parts.Cel.Declarations;
using Great.Parts.Cel.Syntax;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Checking;

// Writes a checked expression as a CheckedExpr message of checked.proto: the reference_map and the type_map, then the tree in the form
// that type checking gives in cel-go (a name that resolves to a declaration is an identifier of the full name, a call of a qualified
// function name is a global call of the full name, a message literal has the full name of its type) and the source info, over the same
// writer as a parsed expression. The macro calls are written in the form of parsing.
internal static class CheckedExprCodec
{
    private const int ReferenceMapField = 2;
    private const int TypeMapField = 3;
    private const int ExprField = 4;
    private const int SourceInfoField = 5;
    private const string DecimalOverloadId = "string_to_decimal";

    /// <summary>
    /// Writes a checked expression.
    /// </summary>
    /// <param name="checkedExpr">The checked expression.</param>
    /// <returns>The bytes of the message.</returns>
    internal static byte[] Write(CelCheckedExpr checkedExpr)
    {
        CheckedExpression expression = checkedExpr.Expression;
        var writer = new ExprWriter(expression.Parsed, new CheckedForm(expression.References));
        return ProtoWriter.Encode(output =>
        {
            writer.Use(output);
            WriteReferences(output, expression.References, writer);
            WriteTypes(output, expression.Types, writer);
            writer.WriteExpr(ExprField, expression.Parsed.Root);
            writer.WriteSourceInfo(SourceInfoField);
        });
    }

    // map<int64, Reference> reference_map, in ascending order of the ids: the references of the check except the fields of object types,
    // and the call that stands for each decimal constant.
    private static void WriteReferences(ProtoWriter output, IReadOnlyDictionary<int, Reference> references, ExprWriter writer)
    {
        var entries = new List<(int Id, Reference? Reference)>(references.Count + writer.DecimalArguments.Count);
        foreach (KeyValuePair<int, Reference> entry in references)
        {
            if (!entry.Value.IsField)
            {
                entries.Add((entry.Key, entry.Value));
            }
        }

        foreach (int constantId in writer.DecimalArguments.Keys)
        {
            entries.Add((constantId, null));
        }

        entries.Sort(static (left, right) => left.Id.CompareTo(right.Id));
        foreach ((int id, Reference? reference) in entries)
        {
            int entryToken = output.Begin(ReferenceMapField, out int entryStart);
            output.Int64Always(1, id);
            int valueToken = output.Begin(2, out int valueStart);
            WriteReference(output, reference);
            output.End(valueToken, valueStart);
            output.End(entryToken, entryStart);
        }
    }

    // A Reference: the name and the overload ids, as cel-go writes them. A call has an empty name, since the written tree has the full
    // name of its function; a name has no overload ids; an enumeration constant has its value.
    private static void WriteReference(ProtoWriter output, Reference? reference)
    {
        if (reference is null)
        {
            output.StringAlways(3, DecimalOverloadId);
            return;
        }

        if (reference.IdentifierName is { } identifier)
        {
            output.String(1, identifier);
            WriteConstantValue(output, reference.Constant);
            return;
        }

        if (reference.Local is { } local)
        {
            output.String(1, local);
            return;
        }

        if (reference.ObjectType is { } objectType)
        {
            output.String(1, objectType.Name);
            return;
        }

        foreach (OverloadBinding candidate in reference.Candidates)
        {
            output.StringAlways(3, candidate.Declaration.OverloadId);
        }
    }

    // value of a reference: the constant with the int64 value.
    private static void WriteConstantValue(ProtoWriter output, long? constant)
    {
        if (constant is not { } value)
        {
            return;
        }

        int token = output.Begin(4, out int start);
        output.Int64Always(3, value);
        output.End(token, start);
    }

    // map<int64, Type> type_map, in ascending order of the ids: the types of the nodes, and the string of the argument of each decimal call.
    private static void WriteTypes(ProtoWriter output, IReadOnlyDictionary<int, CelType> types, ExprWriter writer)
    {
        var entries = new List<(int Id, CelType Type)>(types.Count + writer.DecimalArguments.Count);
        foreach (KeyValuePair<int, CelType> entry in types)
        {
            entries.Add((entry.Key, entry.Value));
        }

        foreach (int argument in writer.DecimalArguments.Values)
        {
            entries.Add((argument, CelType.String));
        }

        entries.Sort(static (left, right) => left.Id.CompareTo(right.Id));
        foreach ((int id, CelType type) in entries)
        {
            int entryToken = output.Begin(TypeMapField, out int entryStart);
            output.Int64Always(1, id);
            TypeWriter.Write(output, 2, type);
            output.End(entryToken, entryStart);
        }
    }

    // The checked form of the tree: what each node of the tree is written as, from the references of the check.
    private sealed class CheckedForm(IReadOnlyDictionary<int, Reference> references) : ExprForm
    {
        internal override bool TryIdentifier(CelExprNode node, [NotNullWhen(true)] out string? name)
        {
            name = null;
            if (node is CelIdentNode or CelSelectNode { IsPresenceTest: false } && references.TryGetValue(node.Id, out Reference? reference))
            {
                name = reference.IdentifierName;
            }

            return name is not null;
        }

        // A global call is written with the full name of its function; a call with a target whose candidates are global overloads is the
        // call of a qualified function name, whose target is part of the name. A call on a receiver is written as it is.
        internal override bool TryCall(CelCallNode call, [NotNullWhen(true)] out string? function, out bool dropTarget)
        {
            function = null;
            dropTarget = false;
            if (!references.TryGetValue(call.Id, out Reference? reference) || reference.Candidates.IsDefaultOrEmpty)
            {
                return false;
            }

            OverloadBinding first = reference.Candidates[0];
            if (call.Target is not null && first.Declaration.IsReceiverStyle)
            {
                return false;
            }

            function = first.Function;
            dropTarget = call.Target is not null;
            return true;
        }

        internal override string MessageName(CelStructNode message) =>
            references.TryGetValue(message.Id, out Reference? reference) && reference is { IsField: false, ObjectType: { } type }
                ? type.Name
                : message.TypeName;
    }
}

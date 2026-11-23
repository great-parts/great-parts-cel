using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel.Syntax;

/// <summary>
/// A parsed expression: the <c>ParsedExpr</c> message of <c>syntax.proto</c> — the root node, the position of each node and
/// entry, and the macro calls as written. It does not refer to an environment.
/// </summary>
/// <remarks>
/// <para>
/// A parsed expression comes from <see cref="CelEnvironment.Parse(string, CelCompileOptions)"/>, from
/// <see cref="CelExprFactory.Build(CelExprNode)"/> — for example a tree that a parser of another language, such as a
/// filter, built — or from protobuf (<see cref="FromProtobuf(ReadOnlySpan{byte}, string)"/>). Any of them compiles in an
/// environment with <see cref="CelEnvironment.CompileParsed{T}(CelParsedExpr, CelCompileOptions)"/>, which type-checks the
/// tree as it type-checks a text; <see cref="CelSyntax.Format(CelParsedExpr)"/> prints it.
/// </para>
/// <para>
/// Positions are offsets in UTF-16 code units in the text the tree belongs to: the text that was parsed, the text given to
/// the factory, or the source text given when loading. <c>syntax.proto</c> counts code points; reading and writing protobuf
/// convert between the two.
/// </para>
/// <para>The parsed expression is immutable and can be shared between threads.</para>
/// </remarks>
public sealed class CelParsedExpr
{
    // The offset of the node or entry with the id i + 1 at the index i: ids run from 1, and a table holds every node and entry its
    // source created, in or out of the tree. An offset of -1 is no position.
    private readonly ImmutableArray<int> _offsets;

    // The offset just after the last token of the node or entry with the id i + 1 at the index i.
    private readonly ImmutableArray<int> _ends;

    // The ids of the expansions of macro calls in ascending order, and the call of each at the same index.
    private readonly ImmutableArray<int> _macroIds;
    private readonly ImmutableArray<CelMacroCall> _macroCalls;

    // The nodes of the tree by id with their parents; made on the first search and kept.
    private TreeIndex? _index;

    internal CelParsedExpr(
        CelExprNode root,
        ImmutableArray<int> offsets,
        ImmutableArray<int> ends,
        ImmutableArray<int> macroIds,
        ImmutableArray<CelMacroCall> macroCalls,
        LineTable lines,
        string? sourceName = null,
        SyntaxOptions? parsedWith = null)
    {
        Root = root;
        _offsets = offsets;
        _ends = ends;
        _macroIds = macroIds;
        _macroCalls = macroCalls;
        Lines = lines;
        SourceName = sourceName;
        ParsedWith = parsedWith;
    }

    /// <summary>
    /// The line table of the text, which turns the offsets of nodes into lines and columns.
    /// </summary>
    internal LineTable Lines { get; }

    /// <summary>
    /// The syntax options of the parser that gave this tree, or <see langword="null"/> for a tree of another source: an
    /// environment whose syntax is the same object knows that the tree needs no check of its syntax.
    /// </summary>
    internal SyntaxOptions? ParsedWith { get; }

    /// <summary>
    /// The macro calls of the tree, written or not, in the order of the ids of their expansions.
    /// </summary>
    internal ImmutableArray<CelMacroCall> MacroCalls => _macroCalls;

    /// <summary>
    /// The ids of the expansions of the macro calls, in ascending order, each at the index of its call in <see cref="MacroCalls"/>.
    /// </summary>
    internal ImmutableArray<int> MacroIds => _macroIds;

    /// <summary>
    /// The number of ids that the source of the tree gave to nodes and entries, in or out of the tree: the greatest id.
    /// </summary>
    internal int IdCount => _offsets.Length;

    /// <summary>
    /// Returns the position of a node or an entry: its offset in UTF-16 code units from the start of the text.
    /// </summary>
    /// <param name="id">The id of a node or an entry of this tree.</param>
    /// <returns>The offset, or -1 for a node without a position.</returns>
    internal int GetOffset(int id) => _offsets[id - 1];

    /// <summary>
    /// Returns the end of a node or an entry: the offset just after its last token, so that the fragment of the node is
    /// <c>[GetOffset(id), GetEnd(id))</c>.
    /// </summary>
    /// <param name="id">The id of a node or an entry of this tree.</param>
    /// <returns>The offset in UTF-16 code units, not less than the offset of the node.</returns>
    internal int GetEnd(int id) => _ends[id - 1];

    /// <summary>
    /// Returns the macro call that a node is the expansion of, written or not.
    /// </summary>
    /// <param name="id">The id of a node of this tree.</param>
    /// <returns>The call, or <see langword="null"/> when the node is not the expansion of a macro call.</returns>
    internal CelMacroCall? GetRecordedMacroCall(int id)
    {
        int index = _macroIds.BinarySearch(id);
        return index >= 0 ? _macroCalls[index] : null;
    }

    /// <summary>
    /// Gives the same tree with a source name and the syntax of the parser that made it.
    /// </summary>
    /// <param name="sourceName">The name of the source document, or <see langword="null"/>.</param>
    /// <param name="parsedWith">The syntax options of the parser.</param>
    /// <returns>The tree.</returns>
    internal CelParsedExpr WithSource(string? sourceName, SyntaxOptions parsedWith) =>
        new(Root, _offsets, _ends, _macroIds, _macroCalls, Lines, sourceName, parsedWith);

    /// <summary>
    /// The root node.
    /// </summary>
    public CelExprNode Root { get; }

    /// <summary>
    /// The name of the source document of the expression — <c>SourceInfo.location</c> of <c>syntax.proto</c> — or
    /// <see langword="null"/>: the source name of the options of parsing, or the location of a loaded message.
    /// </summary>
    public string? SourceName { get; }

    /// <summary>
    /// Returns the fragment of the text that a node or an entry covers: its start, with the line and the column, and its
    /// length.
    /// </summary>
    /// <remarks>
    /// The fragment of a parsed node runs from its position — the token that cel-go takes for the node, such as the operator
    /// of a binary call — to the end of its last token, as the fragment of an error at that node. A node of a loaded message
    /// has the length 0. A node without a position, such as a node that a factory without a text created, has
    /// <c>default(CelSourceSpan)</c>. An id inside the range that the creator of the tree numbered but that is not in the tree, such as
    /// a node that a macro expansion dropped or a node of a factory that did not enter the tree, gives the recorded position or
    /// <c>default(CelSourceSpan)</c>.
    /// </remarks>
    /// <param name="id">
    /// The id of a node or an entry of this tree, or of a node that the creator of the tree numbered and that is not in it.
    /// </param>
    /// <returns>The fragment in UTF-16 code units, without a source name.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The id is outside the range of the ids that the parser, the factory or the loader gave to this tree.
    /// </exception>
    public CelSourceSpan GetSpan(int id)
    {
        if ((uint)(id - 1) >= (uint)_offsets.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(id), id, "No node or entry of this tree has the id.");
        }

        int offset = _offsets[id - 1];
        return offset < 0 ? default : new CelSourceSpan(Lines.GetLocation(offset), _ends[id - 1] - offset);
    }

    /// <summary>
    /// Returns the macro call that a node is the expansion of, as written in the text.
    /// </summary>
    /// <param name="id">The id of a node of this tree.</param>
    /// <returns>The call, or <see langword="null"/> when the node is not the expansion of a macro call written in the text.</returns>
    public CelMacroCall? GetMacroCall(int id) => GetRecordedMacroCall(id) is { IsWritten: true } call ? call : null;

    /// <summary>
    /// Finds the node with an id.
    /// </summary>
    /// <remarks>The index of the nodes by id is built on the first call of this method or <see cref="FindParent(int)"/> and kept.</remarks>
    /// <param name="id">The id.</param>
    /// <returns>The node, or <see langword="null"/> when no node of this tree has the id; an entry is not a node.</returns>
    public CelExprNode? FindNode(int id)
    {
        TreeIndex index = GetIndex();
        return (uint)id < (uint)index.Nodes.Length ? index.Nodes[id] : null;
    }

    /// <summary>
    /// Finds the parent of the node or the entry with an id: the node whose <see cref="CelExprNode.Children"/> holds the node,
    /// or that holds the entry.
    /// </summary>
    /// <param name="id">The id of a node or an entry of this tree.</param>
    /// <returns>The parent, or <see langword="null"/> for the root and for an id that no node or entry of this tree has.</returns>
    public CelExprNode? FindParent(int id)
    {
        TreeIndex index = GetIndex();
        return (uint)id < (uint)index.Nodes.Length && index.Present[id] ? index.Parents[id] : null;
    }

    private TreeIndex GetIndex() => Volatile.Read(ref _index) ?? Interlocked.CompareExchange(ref _index, TreeIndex.Create(Root, _offsets.Length), null) ?? _index!;

    // The nodes of the tree and their parents by id, and the ids that a node or an entry of the tree has. Made by one walk
    // with an explicit stack.
    private sealed class TreeIndex
    {
        private TreeIndex(int length)
        {
            Nodes = new CelExprNode?[length];
            Parents = new CelExprNode?[length];
            Present = new bool[length];
        }

        internal CelExprNode?[] Nodes { get; }

        internal CelExprNode?[] Parents { get; }

        internal bool[] Present { get; }

        internal static TreeIndex Create(CelExprNode root, int count)
        {
            var index = new TreeIndex(count + 1);
            if ((uint)root.Id < (uint)index.Nodes.Length)
            {
                index.Nodes[root.Id] = root;
                index.Present[root.Id] = true;
            }

            Stack<CelExprNode> pending = new();
            pending.Push(root);
            while (pending.Count > 0)
            {
                CelExprNode node = pending.Pop();
                index.AddEntries(node);
                foreach (CelExprNode child in node.Children)
                {
                    if ((uint)child.Id < (uint)index.Nodes.Length)
                    {
                        index.Nodes[child.Id] = child;
                        index.Parents[child.Id] = node;
                        index.Present[child.Id] = true;
                    }

                    pending.Push(child);
                }
            }

            return index;
        }

        private void AddEntries(CelExprNode owner)
        {
            switch (owner)
            {
                case CelMapNode map:
                    foreach (CelMapEntryNode entry in map.Entries)
                    {
                        AddEntry(entry.Id, owner);
                    }

                    break;
                case CelStructNode message:
                    foreach (CelStructFieldNode field in message.Fields)
                    {
                        AddEntry(field.Id, owner);
                    }

                    break;
                default:
                    break;
            }
        }

        private void AddEntry(int id, CelExprNode owner)
        {
            if ((uint)id < (uint)Nodes.Length)
            {
                Parents[id] = owner;
                Present[id] = true;
            }
        }
    }

    /// <summary>
    /// Writes the parsed expression as the binary protobuf form of the message <c>cel.expr.ParsedExpr</c> of cel-spec, which
    /// cel-go and other implementations read.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The library writes the message itself, without a protobuf package. It writes the fields of <c>syntax.proto</c> of
    /// cel-expr v0.25.1: the tree, the positions of the nodes and entries as code points (the start of each node, as cel-go
    /// writes), the starts of the lines, the location and the macro calls in the form of the cel-go parser. A <c>decimal</c>
    /// constant is written as the call <c>decimal("…")</c> with the text of the value, which <c>syntax.proto</c> can hold and
    /// which has the same value in an environment with the <c>decimal</c> extension; loading keeps it a call.
    /// </para>
    /// <para>The same message in the package <c>google.api.expr.v1alpha1</c> has the same fields and reads the same bytes.</para>
    /// </remarks>
    /// <returns>The bytes of the message.</returns>
    /// <exception cref="InvalidOperationException">The tree is too deep to be written for the stack that is left.</exception>
    public byte[] ToProtobuf() => ParsedExprCodec.Write(this);

    /// <summary>
    /// Loads a parsed expression from the binary protobuf form of the message <c>cel.expr.ParsedExpr</c>, as cel-go and this
    /// library write it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The bytes can come from an untrusted source: any content gives a result, not an exception, and the work is linear in
    /// the length of the bytes. Bytes that are not a valid message are <see cref="CelErrorCode.InvalidProtobuf"/>; a tree that
    /// breaks the rules of <c>syntax.proto</c> — an expression without a kind, an empty name, an id used twice, an optional
    /// index outside the list, a position outside <paramref name="sourceText"/> — is
    /// <see cref="CelErrorCode.InvalidSyntaxTree"/>; a tree deeper than the nesting limit of parsing is
    /// <see cref="CelErrorCode.NestingTooDeep"/>. Unknown fields are skipped, as protobuf requires.
    /// </para>
    /// <para>
    /// The ids are renumbered from 1. The positions are code points; with <paramref name="sourceText"/> they are converted to
    /// UTF-16 code units of that text, without it they are taken as UTF-16 code units, which is exact for a text without
    /// characters outside the Basic Multilingual Plane.
    /// </para>
    /// <para>
    /// The target and the arguments of a macro call are copies in the message: a copy whose id is the id of a node of the tree
    /// is that node of the tree, as in a parsed expression; any other copy — the argument <c>m.f</c> of <c>has(m.f)</c> —
    /// is a new node built from it, by the same rule for its children. A macro call must be recorded under the id of a node of
    /// the tree. A call <c>decimal</c> of a string
    /// constant stays a call: it is not turned back into a <c>decimal</c> literal, which only an environment with the literal
    /// option accepts.
    /// </para>
    /// </remarks>
    /// <param name="parsedExpr">The bytes of the message.</param>
    /// <param name="sourceText">The text that the positions of the message refer to, or <see langword="null"/>.</param>
    /// <returns>The parsed expression, or the errors.</returns>
    public static CelParseResult FromProtobuf(ReadOnlySpan<byte> parsedExpr, string? sourceText = null) =>
        ParsedExprCodec.Read(parsedExpr, sourceText, isChecked: false);

    /// <summary>
    /// Loads the parsed expression of a checked expression from the binary protobuf form of the message
    /// <c>cel.expr.CheckedExpr</c>, as cel-go and <c>CelCheckedExpr.ToProtobuf</c> write it: its <c>expr</c> and its
    /// <c>source_info</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>reference_map</c> and the <c>type_map</c> of the message are checked for valid protobuf and not used: a stored
    /// checked expression is compiled with <see cref="CelEnvironment.CompileParsed{T}(CelParsedExpr, CelCompileOptions)"/>,
    /// which type-checks it again in the environment, so that only overloads that type checking found applicable are called.
    /// The rules of the bytes, the errors and the positions are those of <see cref="FromProtobuf(ReadOnlySpan{byte}, string)"/>.
    /// </para>
    /// <para>
    /// The tree is in the form that type checking writes, as cel-go does: an identifier with a fully qualified name such as
    /// <c>acme.limit</c> where the text had a selection chain or a name resolved in a container, a global call of a full
    /// function name such as <c>math.abs</c> where the text had the target <c>math</c>, and the full name of a message type.
    /// Compiling it resolves these names as the text resolves them and gives the result of the text, except for a name that
    /// the container resolves to another declaration when the tree is checked again: a full name such as <c>acme.limit</c>
    /// beside <c>acme.acme.limit</c> in the container <c>acme</c>, and a name that the text wrote with a leading dot and that
    /// the checking wrote without it, as cel-go does, such as <c>.y</c> beside <c>com.example.y</c> in the container
    /// <c>com.example</c>.
    /// </para>
    /// </remarks>
    /// <param name="checkedExpr">The bytes of the message.</param>
    /// <param name="sourceText">The text that the positions of the message refer to, or <see langword="null"/>.</param>
    /// <returns>The parsed expression, or the errors.</returns>
    public static CelParseResult FromCheckedExprProtobuf(ReadOnlySpan<byte> checkedExpr, string? sourceText = null) =>
        ParsedExprCodec.Read(checkedExpr, sourceText, isChecked: true);
}

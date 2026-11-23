using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Syntax;

/// <summary>
/// Creates the nodes of a parsed expression without parsing CEL text: the entry for a parser of another language, such as a
/// filter, for the composition of several expressions into one, and for the rewriting of a tree.
/// </summary>
/// <remarks>
/// <para>
/// Each method creates one node from its children, so children are created before their parent; <see cref="Build(CelExprNode)"/>
/// makes a <see cref="CelParsedExpr"/> of a root. The factory assigns the ids, from 1 in the order of creation, and keeps the
/// position of each node and the macro calls of <see cref="ExpandMacro(string, CelExprNode, ReadOnlySpan{CelExprNode})"/>. The tree
/// compiles with <see cref="CelEnvironment.CompileParsed{T}(CelParsedExpr, CelCompileOptions)"/> exactly as a parsed text
/// that gives the same tree: names, functions and types are resolved by type checking, so a name that the environment does not
/// declare is the compilation error <see cref="Diagnostics.CelErrorCode.UndeclaredReference"/> at the node.
/// </para>
/// <para>
/// Positions: a factory created with a source text gives each node the range of that text in <see cref="Position"/> when the
/// node is created, so that errors of compilation point into the source text, such as the text of a filter. A factory without a
/// text creates nodes without positions; their errors have no place.
/// </para>
/// <para>
/// The factory checks its arguments: a <see langword="null"/> or empty name, a node created by another factory, a constant of
/// a kind that is not a literal, or an optional index outside the list is <see cref="ArgumentException"/>. Within one
/// tree a node can be used only once; <see cref="Build(CelExprNode)"/> checks it. The factory is not safe for use from
/// several threads at once; the trees it builds are immutable and are.
/// </para>
/// </remarks>
public sealed class CelExprFactory
{
    private const string Detached = "The node was not created by this factory.";

    // Creates the nodes and keeps them, to tell the nodes of this factory from foreign ones.
    private readonly NodeFactory _nodes = new(keepCreated: true);

    private readonly LineTable _lines;

    private Range? _position;

    // The offset and the end that the range of Position resolves to in the source text; -1 for no position.
    private int _offset = -1;
    private int _end = -1;

    /// <summary>
    /// Creates a factory whose nodes have no positions.
    /// </summary>
    public CelExprFactory()
    {
        _lines = new LineTable(ReadOnlySpan<char>.Empty);
    }

    /// <summary>
    /// Creates a factory whose nodes have positions in a source text.
    /// </summary>
    /// <param name="sourceText">
    /// The text that the positions refer to, for example the text of a filter that a parser of another language translates.
    /// It is not parsed as CEL.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="sourceText"/> is <see langword="null"/>.</exception>
    public CelExprFactory(string sourceText)
    {
        ArgumentNullException.ThrowIfNull(sourceText);
        SourceText = sourceText;
        _lines = new LineTable(sourceText);
    }

    /// <summary>
    /// The source text of the positions, or <see langword="null"/> for a factory without positions.
    /// </summary>
    public string? SourceText { get; }

    /// <summary>
    /// The range of <see cref="SourceText"/> given to each node and entry created from now on, until the next change; or
    /// <see langword="null"/>, the initial value, for no position.
    /// </summary>
    /// <remarks>
    /// <c>factory.Position = 7..12;</c> before creating the nodes of a term makes an error at any of them point to the
    /// characters 7 to 11 of the source text. All nodes that one call of
    /// <see cref="ExpandMacro(string, CelExprNode, ReadOnlySpan{CelExprNode})"/> creates get the same range, as the nodes
    /// of a macro expansion of a parsed text share the place of the call.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Set to a range in a factory without a source text.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Set to a range outside <see cref="SourceText"/>.</exception>
    public Range? Position
    {
        get => _position;
        set
        {
            if (value is not { } range)
            {
                _position = null;
                _offset = -1;
                _end = -1;
                return;
            }

            if (SourceText is null)
            {
                throw new InvalidOperationException("The factory has no source text, so a node has no position to set.");
            }

            (int offset, int length) = range.GetOffsetAndLength(SourceText.Length);
            _position = range;
            _offset = offset;
            _end = offset + length;
        }
    }

    /// <summary>
    /// Creates a literal.
    /// </summary>
    /// <param name="value">
    /// The value, of the kind <see cref="CelValueKind.Null"/>, <see cref="CelValueKind.Bool"/>, <see cref="CelValueKind.Int"/>,
    /// <see cref="CelValueKind.UInt"/>, <see cref="CelValueKind.Double"/>, <see cref="CelValueKind.String"/>,
    /// <see cref="CelValueKind.Bytes"/> or <see cref="CelValueKind.Decimal"/>.
    /// </param>
    /// <returns>The node.</returns>
    /// <exception cref="ArgumentException">The value is of another kind, such as a list or a timestamp, which has no literal.</exception>
    public CelConstantNode Constant(CelValue value)
    {
        if (value.Kind is not (CelValueKind.Null or CelValueKind.Bool or CelValueKind.Int or CelValueKind.UInt or CelValueKind.Double
            or CelValueKind.String or CelValueKind.Bytes or CelValueKind.Decimal))
        {
            throw new ArgumentException("A value of the kind " + value.Kind + " has no literal.", nameof(value));
        }

        return _nodes.Constant(value, At());
    }

    /// <summary>
    /// Creates an identifier.
    /// </summary>
    /// <param name="name">
    /// The name, a leading dot allowed; type checking resolves it. Internal names that start with <c>@</c>, such as the
    /// accumulator <c>@result</c> or the index variables of <c>cel.@block</c>, are allowed.
    /// </param>
    /// <returns>The node.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty.</exception>
    public CelIdentNode Ident(string name)
    {
        NotEmpty(name, nameof(name));
        return _nodes.Ident(name, At());
    }

    /// <summary>
    /// Creates a field selection <c>operand.field</c>.
    /// </summary>
    /// <param name="operand">The expression whose field is selected.</param>
    /// <param name="field">The name of the field.</param>
    /// <returns>The node.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="field"/> is empty, or <paramref name="operand"/> belongs to another factory.</exception>
    public CelSelectNode Select(CelExprNode operand, string field)
    {
        Owned(operand, nameof(operand));
        NotEmpty(field, nameof(field));
        return _nodes.Select(operand, field, isPresenceTest: false, At());
    }

    /// <summary>
    /// Creates the presence test <c>has(operand.field)</c> without recording a macro call: a selection with
    /// <see cref="CelSelectNode.IsPresenceTest"/>. To record the call, so that the tree prints as <c>has(…)</c>, use
    /// <see cref="ExpandMacro(string, CelExprNode, ReadOnlySpan{CelExprNode})"/> with <c>has</c>.
    /// </summary>
    /// <param name="operand">The expression whose field is tested.</param>
    /// <param name="field">The name of the field.</param>
    /// <returns>The node.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="field"/> is empty, or <paramref name="operand"/> belongs to another factory.</exception>
    public CelSelectNode PresenceTest(CelExprNode operand, string field)
    {
        Owned(operand, nameof(operand));
        NotEmpty(field, nameof(field));
        return _nodes.Select(operand, field, isPresenceTest: true, At());
    }

    /// <summary>
    /// Creates a global call of a function or an operator, such as <c>size(x)</c> or <c>_+_(a, b)</c>.
    /// </summary>
    /// <param name="function">The name of the function, such as <c>size</c>, <c>_==_</c> or <c>timestamp</c>.</param>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The node.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="function"/> or an argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="function"/> is empty, or an argument belongs to another factory.</exception>
    public CelCallNode Call(string function, params ReadOnlySpan<CelExprNode> arguments)
    {
        NotEmpty(function, nameof(function));
        ImmutableArray<CelExprNode> owned = OwnedAll(arguments, nameof(arguments));
        return _nodes.Call(function, null, owned, At());
    }

    /// <summary>
    /// Creates a call in the member form <c>target.function(arguments)</c>, such as <c>s.startsWith("a")</c>.
    /// </summary>
    /// <param name="function">The name of the function.</param>
    /// <param name="target">The receiver.</param>
    /// <param name="arguments">The arguments after the receiver.</param>
    /// <returns>The node.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="function"/>, <paramref name="target"/> or an argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="function"/> is empty, or a node belongs to another factory.</exception>
    public CelCallNode MemberCall(string function, CelExprNode target, params ReadOnlySpan<CelExprNode> arguments)
    {
        NotEmpty(function, nameof(function));
        Owned(target, nameof(target));
        ImmutableArray<CelExprNode> owned = OwnedAll(arguments, nameof(arguments));
        return _nodes.Call(function, target, owned, At());
    }

    /// <summary>
    /// Creates a list literal.
    /// </summary>
    /// <param name="elements">The elements.</param>
    /// <param name="optionalIndices">The indices of the optional elements, <c>?e</c>, each once; empty for none.</param>
    /// <returns>The node.</returns>
    /// <exception cref="ArgumentNullException">An element is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">An element belongs to another factory.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An optional index is outside the elements or repeats.</exception>
    public CelListNode List(ReadOnlySpan<CelExprNode> elements, ReadOnlySpan<int> optionalIndices = default)
    {
        ImmutableArray<CelExprNode> owned = OwnedAll(elements, nameof(elements));
        int[] indices = optionalIndices.ToArray();
        Array.Sort(indices);
        for (int i = 0; i < indices.Length; i++)
        {
            if (indices[i] < 0 || indices[i] >= owned.Length || (i > 0 && indices[i] == indices[i - 1]))
            {
                throw new ArgumentOutOfRangeException(nameof(optionalIndices), indices[i], "An optional index is outside the elements or repeats.");
            }
        }

        return _nodes.List(owned, [.. indices], At());
    }

    /// <summary>
    /// Creates a map literal from entries created by <see cref="MapEntry(CelExprNode, CelExprNode, bool)"/>.
    /// </summary>
    /// <param name="entries">The entries.</param>
    /// <returns>The node.</returns>
    /// <exception cref="ArgumentNullException">An entry is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">An entry belongs to another factory.</exception>
    public CelMapNode Map(ReadOnlySpan<CelMapEntryNode> entries)
    {
        ImmutableArray<CelMapEntryNode>.Builder owned = ImmutableArray.CreateBuilder<CelMapEntryNode>(entries.Length);
        foreach (CelMapEntryNode entry in entries)
        {
            ArgumentNullException.ThrowIfNull(entry, nameof(entries));
            Owned(entry.Id, entry, nameof(entries));
            owned.Add(entry);
        }

        return _nodes.Map(owned.MoveToImmutable(), At());
    }

    /// <summary>
    /// Creates an entry of a map literal.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="value">The value.</param>
    /// <param name="isOptional">Whether the entry is optional, <c>?key: value</c>.</param>
    /// <returns>The entry.</returns>
    /// <exception cref="ArgumentNullException">A node is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A node belongs to another factory.</exception>
    public CelMapEntryNode MapEntry(CelExprNode key, CelExprNode value, bool isOptional = false)
    {
        Owned(key, nameof(key));
        Owned(value, nameof(value));
        return _nodes.Entry(key, value, isOptional, At());
    }

    /// <summary>
    /// Creates a message literal from fields created by <see cref="StructField(string, CelExprNode, bool)"/>.
    /// </summary>
    /// <param name="typeName">The name of the type, resolved by type checking in the container of the environment.</param>
    /// <param name="fields">The fields.</param>
    /// <returns>The node.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="typeName"/> or a field is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="typeName"/> is empty, or a field belongs to another factory.</exception>
    public CelStructNode Struct(string typeName, ReadOnlySpan<CelStructFieldNode> fields)
    {
        NotEmpty(typeName, nameof(typeName));
        ImmutableArray<CelStructFieldNode>.Builder owned = ImmutableArray.CreateBuilder<CelStructFieldNode>(fields.Length);
        foreach (CelStructFieldNode field in fields)
        {
            ArgumentNullException.ThrowIfNull(field, nameof(fields));
            Owned(field.Id, field, nameof(fields));
            owned.Add(field);
        }

        return _nodes.Struct(typeName, owned.MoveToImmutable(), At());
    }

    /// <summary>
    /// Creates a field of a message literal.
    /// </summary>
    /// <param name="name">The name of the field.</param>
    /// <param name="value">The value.</param>
    /// <param name="isOptional">Whether the field is optional, <c>?name: value</c>.</param>
    /// <returns>The field.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty, or <paramref name="value"/> belongs to another factory.</exception>
    public CelStructFieldNode StructField(string name, CelExprNode value, bool isOptional = false)
    {
        NotEmpty(name, nameof(name));
        Owned(value, nameof(value));
        return _nodes.Field(name, value, isOptional, At());
    }

    /// <summary>
    /// Creates a comprehension of one iteration variable. A macro call is not recorded; to create the expansion of a macro,
    /// use <see cref="ExpandMacro(string, CelExprNode, ReadOnlySpan{CelExprNode})"/>.
    /// </summary>
    /// <param name="iterVar">The name of the iteration variable.</param>
    /// <param name="iterRange">The range.</param>
    /// <param name="accuVar">The name of the accumulator.</param>
    /// <param name="accuInit">The initial value of the accumulator.</param>
    /// <param name="loopCondition">The condition evaluated before each step.</param>
    /// <param name="loopStep">The step.</param>
    /// <param name="result">The result.</param>
    /// <returns>The node.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A name is empty, the two names are equal, or a node belongs to another factory.</exception>
    public CelComprehensionNode Comprehension(
        string iterVar,
        CelExprNode iterRange,
        string accuVar,
        CelExprNode accuInit,
        CelExprNode loopCondition,
        CelExprNode loopStep,
        CelExprNode result)
    {
        NotEmpty(iterVar, nameof(iterVar));
        NotEmpty(accuVar, nameof(accuVar));
        if (string.Equals(iterVar, accuVar, StringComparison.Ordinal))
        {
            throw new ArgumentException("The iteration variable and the accumulator have one name.", nameof(accuVar));
        }

        OwnedAll(iterRange, accuInit, loopCondition, loopStep, result);
        return _nodes.Comprehension(iterVar, iterRange, accuVar, accuInit, loopCondition, loopStep, result, At());
    }

    /// <summary>
    /// Creates a comprehension of two iteration variables: the index and the element of a list, the key and the value of a map.
    /// </summary>
    /// <param name="iterVar">The name of the first iteration variable.</param>
    /// <param name="iterVar2">The name of the second iteration variable.</param>
    /// <param name="iterRange">The range.</param>
    /// <param name="accuVar">The name of the accumulator.</param>
    /// <param name="accuInit">The initial value of the accumulator.</param>
    /// <param name="loopCondition">The condition evaluated before each step.</param>
    /// <param name="loopStep">The step.</param>
    /// <param name="result">The result.</param>
    /// <returns>The node.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A name is empty, two names are equal, or a node belongs to another factory.</exception>
    public CelComprehensionNode Comprehension(
        string iterVar,
        string iterVar2,
        CelExprNode iterRange,
        string accuVar,
        CelExprNode accuInit,
        CelExprNode loopCondition,
        CelExprNode loopStep,
        CelExprNode result)
    {
        NotEmpty(iterVar, nameof(iterVar));
        NotEmpty(iterVar2, nameof(iterVar2));
        NotEmpty(accuVar, nameof(accuVar));
        if (string.Equals(iterVar, iterVar2, StringComparison.Ordinal) || string.Equals(iterVar, accuVar, StringComparison.Ordinal)
            || string.Equals(iterVar2, accuVar, StringComparison.Ordinal))
        {
            throw new ArgumentException("The iteration variables and the accumulator have distinct names.", nameof(iterVar2));
        }

        OwnedAll(iterRange, accuInit, loopCondition, loopStep, result);
        return _nodes.Comprehension(iterVar, iterVar2, iterRange, accuVar, accuInit, loopCondition, loopStep, result, At());
    }

    /// <summary>
    /// Expands a macro of the library as the parser expands its call, and records the call, so that the tree is the tree of
    /// the text of the call and prints as that text.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The macros are those of the standard library — <c>has</c>, <c>all</c>, <c>exists</c>, <c>exists_one</c>, <c>map</c>
    /// with two or three arguments, <c>filter</c> — and those of the extension sets, such as <c>cel.bind</c>,
    /// <c>math.greatest</c>, <c>existsOne</c> or <c>optMap</c>, by the name that a text calls them by. The expansion does not
    /// depend on the environment: an expansion that calls a function the environment does not have is a compilation error, as
    /// for a text.
    /// </para>
    /// <para>
    /// The arguments must be as the macro requires: for example, the first argument of <c>all</c> an identifier, the argument of
    /// <c>has</c> a selection. They are used as the parser uses them: an argument the expansion drops is not part of the tree.
    /// </para>
    /// </remarks>
    /// <param name="name">The name of the macro, such as <c>exists</c> or <c>cel.bind</c>.</param>
    /// <param name="target">The receiver of a macro in the member form, such as <c>items</c> in <c>items.exists(x, p)</c>; <see langword="null"/> for a global macro.</param>
    /// <param name="arguments">The arguments.</param>
    /// <returns>The root of the expansion: a comprehension, a selection of <c>has</c> or a call.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or an argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// No macro of the library has the name, the number of arguments and the style — global or member; an argument is not as
    /// the macro requires, with the message of <see cref="Diagnostics.CelErrorCode.InvalidMacroCall"/>; or a node belongs to
    /// another factory.
    /// </exception>
    public CelExprNode ExpandMacro(string name, CelExprNode? target, params ReadOnlySpan<CelExprNode> arguments)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (target is not null)
        {
            Owned(target, nameof(target));
        }

        ImmutableArray<CelExprNode> owned = OwnedAll(arguments, nameof(arguments));
        Macro macro = MacroTable.All.FindQualified(name, owned.Length, hasTarget: target is not null)
            ?? throw new ArgumentException(
                "No macro of the library is called '" + name + "' with " + owned.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + " arguments " + (target is null ? "in the global form." : "in the member form."),
                nameof(name));

        // The target of a macro of a namespace is the identifier of the namespace, which the call records and the expansion drops.
        CelExprNode? receiver = macro.Namespace is null ? target : _nodes.Ident(macro.Namespace, At());
        MacroCheck check = macro.Check(receiver, owned);
        if (check.IsNotApplicable)
        {
            throw new ArgumentException("The macro '" + name + "' does not apply to this call.", nameof(target));
        }

        if (check.IsError)
        {
            throw new ArgumentException(
                nameof(CelErrorCode.InvalidMacroCall) + ": invalid call of macro '" + macro.Name + "': " + check.Detail,
                nameof(arguments));
        }

        CelExprNode expansion = macro.Expand(_nodes, receiver, owned, At());
        _nodes.RecordMacroCall(expansion, macro.Recorded(receiver, owned));
        return expansion;
    }

    /// <summary>
    /// Copies a subtree of another parsed expression into this factory, with new ids, the macro calls of the copied nodes and,
    /// optionally, nodes replaced: the way to rewrite a tree and to compose expressions parsed from several texts into one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The copy walks the subtree from the top. For each node <paramref name="replace"/> is asked first: a node it returns,
    /// created by this factory, takes the place of the subtree; <see langword="null"/> copies the node and walks its children.
    /// The macro call of a copied node is copied with it; a replaced node loses its call, so a tree with a rewritten macro
    /// prints as its expansion parts.
    /// </para>
    /// <para>
    /// Positions: without <paramref name="mapOffset"/> the copied nodes keep their positions, for a source parsed from the same
    /// text as <see cref="SourceText"/>; with it, each start and end offset of the source is mapped to an offset of
    /// <see cref="SourceText"/>, for example from an expression of a document to the document. A factory without a source text
    /// copies no positions.
    /// </para>
    /// </remarks>
    /// <param name="source">The parsed expression that holds <paramref name="node"/>.</param>
    /// <param name="node">The root of the subtree to copy.</param>
    /// <param name="replace">Gives the node that replaces a node of the source, or <see langword="null"/> to copy it; <see langword="null"/> for a plain copy.</param>
    /// <param name="mapOffset">Maps an offset of the source text to an offset of <see cref="SourceText"/>; <see langword="null"/> to keep the offsets.</param>
    /// <returns>The copy of <paramref name="node"/>, or its replacement.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="node"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="node"/> is not a node of <paramref name="source"/>, a replacement belongs to another factory, or a mapped
    /// offset is outside <see cref="SourceText"/>.
    /// </exception>
    public CelExprNode Copy(
        CelParsedExpr source,
        CelExprNode node,
        Func<CelExprNode, CelExprNode?>? replace = null,
        Func<int, int>? mapOffset = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(node);
        if (!ReferenceEquals(source.FindNode(node.Id), node))
        {
            throw new ArgumentException("The node is not a node of the source.", nameof(node));
        }

        return new Copier(this, source, replace, mapOffset).Run(node);
    }

    /// <summary>
    /// Makes a parsed expression of a root created by this factory: the subtree of the root with the positions and the macro
    /// calls of its nodes. The factory can go on creating nodes and build other trees.
    /// </summary>
    /// <param name="root">The root.</param>
    /// <returns>The parsed expression, whose <see cref="CelParsedExpr.SourceName"/> is <see langword="null"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="root"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A node of the subtree belongs to another factory, or a node occurs in it twice.</exception>
    public CelParsedExpr Build(CelExprNode root)
    {
        ArgumentNullException.ThrowIfNull(root);

        // One bit per id: the nodes and entries of the subtree, each once.
        bool[] seen = new bool[_nodes.Count + 1];
        Stack<CelExprNode> pending = new();
        pending.Push(root);
        while (pending.Count > 0)
        {
            CelExprNode node = pending.Pop();
            Visited(seen, node.Id, node, nameof(root));
            switch (node)
            {
                case CelMapNode map:
                    foreach (CelMapEntryNode entry in map.Entries)
                    {
                        Visited(seen, entry.Id, entry, nameof(root));
                    }

                    break;
                case CelStructNode message:
                    foreach (CelStructFieldNode field in message.Fields)
                    {
                        Visited(seen, field.Id, field, nameof(root));
                    }

                    break;
                default:
                    break;
            }

            foreach (CelExprNode child in node.Children)
            {
                pending.Push(child);
            }
        }

        return _nodes.Build(root, _lines, seen);
    }

    // Sets the position of the next node: the offset, and the end that the node takes.
    private int At()
    {
        _nodes.PreviousEnd = _end;
        return _offset;
    }

    private static void NotEmpty(string value, string parameter)
    {
        ArgumentNullException.ThrowIfNull(value, parameter);
        if (value.Length == 0)
        {
            throw new ArgumentException("The name is empty.", parameter);
        }
    }

    private void Owned(CelExprNode node, string parameter)
    {
        ArgumentNullException.ThrowIfNull(node, parameter);
        Owned(node.Id, node, parameter);
    }

    private void Owned(int id, object item, string parameter)
    {
        if (!_nodes.Owns(id, item))
        {
            throw new ArgumentException(Detached, parameter);
        }
    }

    private void OwnedAll(CelExprNode first, CelExprNode second, CelExprNode third, CelExprNode fourth, CelExprNode fifth)
    {
        Owned(first, "iterRange");
        Owned(second, "accuInit");
        Owned(third, "loopCondition");
        Owned(fourth, "loopStep");
        Owned(fifth, "result");
    }

    private ImmutableArray<CelExprNode> OwnedAll(ReadOnlySpan<CelExprNode> nodes, string parameter)
    {
        ImmutableArray<CelExprNode>.Builder owned = ImmutableArray.CreateBuilder<CelExprNode>(nodes.Length);
        foreach (CelExprNode node in nodes)
        {
            Owned(node, parameter);
            owned.Add(node);
        }

        return owned.MoveToImmutable();
    }

    // A node or an entry of the subtree that Build walks: of this factory, and not seen before.
    private void Visited(bool[] seen, int id, object item, string parameter)
    {
        Owned(id, item, parameter);
        if (seen[id])
        {
            throw new ArgumentException("The node " + id.ToString(System.Globalization.CultureInfo.InvariantCulture) + " occurs in the tree twice.", parameter);
        }

        seen[id] = true;
    }

    // The copy of a subtree: the nodes are created after their children, with an explicit stack.
    private sealed class Copier(CelExprFactory factory, CelParsedExpr source, Func<CelExprNode, CelExprNode?>? replace, Func<int, int>? mapOffset)
    {
        // The copy or the replacement of each node of the source reached so far, by the id in the source.
        private readonly Dictionary<int, CelExprNode> _copies = [];

        internal CelExprNode Run(CelExprNode root)
        {
            Stack<(CelExprNode Node, bool Exit)> pending = new();
            pending.Push((root, false));
            while (pending.Count > 0)
            {
                (CelExprNode node, bool exit) = pending.Pop();
                if (_copies.ContainsKey(node.Id))
                {
                    continue;
                }

                if (!exit)
                {
                    if (replace?.Invoke(node) is { } replacement)
                    {
                        factory.Owned(replacement, nameof(replace));
                        _copies[node.Id] = replacement;
                        continue;
                    }

                    pending.Push((node, true));
                    ImmutableArray<CelExprNode> children = node.Children;
                    for (int i = children.Length - 1; i >= 0; i--)
                    {
                        pending.Push((children[i], false));
                    }

                    continue;
                }

                CelExprNode copy = Create(node);
                _copies[node.Id] = copy;
                CopyMacroCall(node, copy);
            }

            return _copies[root.Id];
        }

        // The node with the copies of its children, at the position of the source node.
        private CelExprNode Create(CelExprNode node)
        {
            NodeFactory nodes = factory._nodes;
            switch (node)
            {
                case CelConstantNode constant:
                    return nodes.Constant(constant.Value, At(node.Id));
                case CelIdentNode identifier:
                    return nodes.Ident(identifier.Name, At(node.Id));
                case CelSelectNode select:
                    return nodes.Select(_copies[select.Operand.Id], select.Field, select.IsPresenceTest, At(node.Id));
                case CelCallNode call:
                    return nodes.Call(call.Function, call.Target is null ? null : _copies[call.Target.Id], Copies(call.Arguments), At(node.Id));
                case CelListNode list:
                    return nodes.List(Copies(list.Elements), list.OptionalIndices, At(node.Id));
                case CelMapNode map:
                    ImmutableArray<CelMapEntryNode>.Builder entries = ImmutableArray.CreateBuilder<CelMapEntryNode>(map.Entries.Length);
                    foreach (CelMapEntryNode entry in map.Entries)
                    {
                        entries.Add(nodes.Entry(_copies[entry.Key.Id], _copies[entry.Value.Id], entry.IsOptional, At(entry.Id)));
                    }

                    return nodes.Map(entries.MoveToImmutable(), At(node.Id));
                case CelStructNode message:
                    ImmutableArray<CelStructFieldNode>.Builder fields = ImmutableArray.CreateBuilder<CelStructFieldNode>(message.Fields.Length);
                    foreach (CelStructFieldNode field in message.Fields)
                    {
                        fields.Add(nodes.Field(field.Name, _copies[field.Value.Id], field.IsOptional, At(field.Id)));
                    }

                    return nodes.Struct(message.TypeName, fields.MoveToImmutable(), At(node.Id));
                default:
                    var comprehension = (CelComprehensionNode)node;
                    CelExprNode range = _copies[comprehension.IterRange.Id];
                    CelExprNode init = _copies[comprehension.AccuInit.Id];
                    CelExprNode condition = _copies[comprehension.LoopCondition.Id];
                    CelExprNode step = _copies[comprehension.LoopStep.Id];
                    CelExprNode result = _copies[comprehension.Result.Id];
                    int offset = At(node.Id);
                    return comprehension.IterVar2 is { } iterVar2
                        ? nodes.Comprehension(comprehension.IterVar, iterVar2, range, comprehension.AccuVar, init, condition, step, result, offset)
                        : nodes.Comprehension(comprehension.IterVar, range, comprehension.AccuVar, init, condition, step, result, offset);
            }
        }

        private ImmutableArray<CelExprNode> Copies(ImmutableArray<CelExprNode> nodes)
        {
            ImmutableArray<CelExprNode>.Builder copies = ImmutableArray.CreateBuilder<CelExprNode>(nodes.Length);
            foreach (CelExprNode node in nodes)
            {
                copies.Add(_copies[node.Id]);
            }

            return copies.MoveToImmutable();
        }

        // The macro call of a copied node, with its target and arguments as copies: a node of the subtree is its copy, any other
        // node, such as the argument m.f of has(m.f), is copied on its own.
        private void CopyMacroCall(CelExprNode node, CelExprNode copy)
        {
            if (source.GetRecordedMacroCall(node.Id) is not { } call)
            {
                return;
            }

            CelExprNode? target = call.Target is null ? null : Resolved(call.Target);
            ImmutableArray<CelExprNode>.Builder arguments = ImmutableArray.CreateBuilder<CelExprNode>(call.Arguments.Length);
            foreach (CelExprNode argument in call.Arguments)
            {
                arguments.Add(Resolved(argument));
            }

            factory._nodes.RecordMacroCall(copy, new CelMacroCall(call.Function, target, arguments.MoveToImmutable(), call.IsWritten));
        }

        private CelExprNode Resolved(CelExprNode node) => _copies.TryGetValue(node.Id, out CelExprNode? copy) ? copy : Run(node);

        // The offset of the source node or entry in the source text of the factory, and its end as the end of the next node; -1 when
        // the factory has no text or the node has no position.
        private int At(int id)
        {
            int offset = source.GetOffset(id);
            if (factory.SourceText is null || offset < 0)
            {
                factory._nodes.PreviousEnd = -1;
                return -1;
            }

            int end = source.GetEnd(id);
            if (mapOffset is not null)
            {
                offset = Mapped(offset, nameof(mapOffset));
                end = Mapped(end, nameof(mapOffset));
            }

            factory._nodes.PreviousEnd = end;
            return offset;
        }

        private int Mapped(int offset, string parameter)
        {
            int mapped = mapOffset!(offset);
            if ((uint)mapped > (uint)factory.SourceText!.Length)
            {
                throw new ArgumentException("The offset " + mapped.ToString(System.Globalization.CultureInfo.InvariantCulture) + " is outside the source text.", parameter);
            }

            return mapped;
        }
    }
}

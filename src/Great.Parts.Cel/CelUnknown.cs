using System.Collections.Immutable;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel;

/// <summary>
/// One unknown part that the result of a partial evaluation depends on: an element of <c>UnknownSet</c> of
/// <c>eval.proto</c> with its place — an attribute that matched a pattern of unknown data, or an asynchronous call not yet made.
/// </summary>
/// <remarks>
/// <para>
/// For an attribute, <see cref="Attribute"/> is set and the call members are empty. For an asynchronous call of
/// <see cref="CelProgram{T}.EvaluatePartial"/>, <see cref="Function"/>, <see cref="OverloadId"/> and <see cref="Arguments"/>
/// describe the request the expression needs, and <see cref="Attribute"/> is <see langword="null"/>.
/// </para>
/// <para>The unknown is immutable and can be shared between threads.</para>
/// </remarks>
public sealed class CelUnknown
{
    internal CelUnknown(UnknownEntry entry, CelSourceSpan span)
    {
        ExprId = entry.Id;
        Span = span;
        Attribute = entry.Variable is { } variable ? CelAttributePattern.OfTrail(variable, entry.Qualifiers) : null;
        Function = entry.Function;
        OverloadId = entry.OverloadId;
        Arguments = entry.Arguments;
    }

    /// <summary>
    /// The id of the node of the parsed expression of the program where the value became unknown: a node of the tree that
    /// <c>CelEnvironment.Parse</c> gives for the text of the program in its environment, or of the tree given to
    /// <c>CelEnvironment.CompileParsed</c>.
    /// </summary>
    public int ExprId { get; }

    /// <summary>
    /// The place of the node in the text of the expression, in UTF-16 code units, as the place of an error.
    /// </summary>
    public CelSourceSpan Span { get; }

    /// <summary>
    /// The unknown attribute: the variable and the qualifiers of the access up to the length of the pattern it matched, with no
    /// qualifier that matches any; <see langword="null"/> for an asynchronous call.
    /// </summary>
    public CelAttributePattern? Attribute { get; }

    /// <summary>
    /// The name of the asynchronous function not yet called; <see langword="null"/> for an attribute.
    /// </summary>
    public string? Function { get; }

    /// <summary>
    /// The identifier of the overload of the asynchronous call; <see langword="null"/> for an attribute.
    /// </summary>
    public string? OverloadId { get; }

    /// <summary>
    /// The arguments of the asynchronous call, receiver first; empty for an attribute.
    /// </summary>
    public ImmutableArray<CelValue> Arguments { get; }
}

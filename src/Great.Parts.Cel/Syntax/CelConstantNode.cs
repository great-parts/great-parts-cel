using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Syntax;

/// <summary>
/// A literal: the <c>Constant</c> message of <c>syntax.proto</c>, such as <c>42</c>, <c>1u</c>, <c>2.5</c>, <c>"text"</c>,
/// <c>b"\x00"</c>, <c>true</c> or <c>null</c>.
/// </summary>
/// <remarks>
/// <para>
/// A negative number literal such as <c>-5</c> is one constant, as in cel-go. A <c>decimal</c> literal such as
/// <c>12.34m</c>, which the environment accepts only with its literal option, is a constant of the kind
/// <see cref="CelValueKind.Decimal"/>; <c>syntax.proto</c> has no such constant, so writing the tree to protobuf writes it
/// as the call <c>decimal("12.34")</c>, and loading the bytes gives that call, with the same value, not the constant.
/// </para>
/// <para>The node is immutable and can be read from any number of threads at once.</para>
/// </remarks>
public sealed class CelConstantNode : CelExprNode
{
    internal CelConstantNode(int id, CelValue value)
        : base(id, CelExprKind.Constant)
    {
        Value = value;
    }

    /// <summary>
    /// The value of the literal, of the kind <see cref="CelValueKind.Null"/>, <see cref="CelValueKind.Bool"/>,
    /// <see cref="CelValueKind.Int"/>, <see cref="CelValueKind.UInt"/>, <see cref="CelValueKind.Double"/>,
    /// <see cref="CelValueKind.String"/>, <see cref="CelValueKind.Bytes"/> or <see cref="CelValueKind.Decimal"/>; a tree
    /// loaded from protobuf can also hold the deprecated <c>duration_value</c> and <c>timestamp_value</c> of
    /// <c>syntax.proto</c> as <see cref="CelValueKind.Duration"/> and <see cref="CelValueKind.Timestamp"/>.
    /// </summary>
    public CelValue Value { get; }
}

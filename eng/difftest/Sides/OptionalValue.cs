using System;
using Cel.Expr;
using Any = Google.Protobuf.WellKnownTypes.Any;

namespace Great.Parts.Cel.Eng.Difftest.Sides;

/// <summary>
/// An optional value as a <c>cel.expr.Value</c> (docs/design/eng-difftest.md, "Profile of the set optional"). That message has no kind
/// for an optional, so the library side writes one as an <c>object_value</c> whose <c>Any</c> has a type URL of its own: the empty
/// optional <c>optional.none()</c> is <c>difftest.optional/none</c> with no content, and an optional that holds a value is the held
/// value, <c>null</c> included, as a <c>cel.expr.Value</c> message under the prefix <c>difftest.optional</c>. So the three values that an
/// optional can be — empty, <c>optional.of(null)</c> and a held value — differ by their content and nothing else. The oracle never
/// gives such a value: <c>cel.ValueAsProto</c> of cel-go ends in an error for an empty optional and gives the held value as an
/// <c>Any</c> for the other, so the generator keeps an optional node out of every place that either side returns.
/// </summary>
internal static class OptionalValue
{
    private const string Prefix = "difftest.optional";
    private const string NoneUrl = Prefix + "/none";
    private const string Marker = Prefix + "/";

    /// <summary>The empty optional.</summary>
    internal static Value Empty() => new() { ObjectValue = new Any { TypeUrl = NoneUrl } };

    /// <summary>The optional that holds a value.</summary>
    /// <param name="held">The held value, which can be <c>null</c>.</param>
    internal static Value Of(Value held) => new() { ObjectValue = Any.Pack(held, Prefix) };

    /// <summary>Whether a value is an optional of the form of this type.</summary>
    /// <param name="value">The value.</param>
    internal static bool Is(Value value) => value.KindCase == Value.KindOneofCase.ObjectValue && Is(value.ObjectValue);

    /// <summary>Whether an <c>Any</c> is an optional of the form of this type.</summary>
    /// <param name="any">The message.</param>
    internal static bool Is(Any any) => any.TypeUrl.StartsWith(Marker, StringComparison.Ordinal);

    /// <summary>Reads an optional of the form of this type.</summary>
    /// <param name="any">The message, which <see cref="Is(Any)"/> accepts.</param>
    /// <param name="held">The held value; <see langword="null"/> for an empty optional.</param>
    /// <returns>Whether the optional holds a value.</returns>
    internal static bool TryRead(Any any, out Value? held)
    {
        if (any.TypeUrl == NoneUrl)
        {
            held = null;
            return false;
        }

        held = any.Unpack<Value>();
        return true;
    }
}

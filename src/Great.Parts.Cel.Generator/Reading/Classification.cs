using Microsoft.CodeAnalysis;

namespace Great.Parts.Cel.Generator;

internal enum ClassificationKind : byte
{
    // The type is read: Shape tells how.
    Ok,

    // The type cannot be read: Text is the reason.
    Refused,

    // The type is System.Decimal or Decimal128 (or has one inside) and the compilation has no Decimal128.
    DecimalUnavailable,

    // The type is described, but its default name is not a CEL qualified name: Text is the name.
    InvalidName,

    // A new object type is over the limit of the closure.
    LimitReached,

    // The root is an object type or an enumeration: Symbol is the type.
    RootObject,

    RootEnum,
}

// The result of classifying a type: how it is read, or why it is not.
internal sealed class Classification
{
    internal static readonly Classification Decimal = new(ClassificationKind.DecimalUnavailable, null, string.Empty, null);

    internal static readonly Classification Limit = new(ClassificationKind.LimitReached, null, string.Empty, null);

    private Classification(ClassificationKind kind, Shape? shape, string text, INamedTypeSymbol? symbol)
    {
        Kind = kind;
        Shape = shape;
        Text = text;
        Symbol = symbol;
    }

    internal ClassificationKind Kind { get; }

    internal Shape? Shape { get; }

    internal string Text { get; }

    internal INamedTypeSymbol? Symbol { get; }

    internal static Classification Ok(Shape shape) => new(ClassificationKind.Ok, shape, string.Empty, null);

    internal static Classification Refuse(string reason) => new(ClassificationKind.Refused, null, reason, null);

    internal static Classification BadName(string name) => new(ClassificationKind.InvalidName, null, name, null);

    internal static Classification RootObject(INamedTypeSymbol symbol) => new(ClassificationKind.RootObject, null, string.Empty, symbol);

    internal static Classification RootEnum(INamedTypeSymbol symbol) => new(ClassificationKind.RootEnum, null, string.Empty, symbol);
}

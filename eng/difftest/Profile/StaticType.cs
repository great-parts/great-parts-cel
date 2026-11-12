namespace Great.Parts.Cel.Eng.Difftest.Profile;

/// <summary>
/// The static type that the generator knows of a node it built (docs/design/types.md). It is a bound from below: the type
/// that the checkers give is this type or this type with some parts replaced by <c>dyn</c>, since a call with a <c>dyn</c>
/// argument may get the type <c>dyn</c> from several candidates. <c>dyn</c> here means that the checkers give <c>dyn</c>.
/// <see langword="null"/> — in a parameter, or as the whole type — is a type the generator does not know, such as the
/// element of an empty list literal, whose free type variable the rest of the expression may bind.
/// </summary>
/// <param name="Kind">The kind: a target type of the profile, not a type parameter.</param>
/// <param name="Element">The element type of a list.</param>
/// <param name="Key">The key type of a map.</param>
/// <param name="Value">The value type of a map.</param>
internal sealed record StaticType(ProfileType Kind, StaticType? Element = null, StaticType? Key = null, StaticType? Value = null)
{
    /// <summary><c>dyn</c>.</summary>
    internal static StaticType Dyn { get; } = new(ProfileType.DynType);

    /// <summary>Whether the type is <c>dyn</c>.</summary>
    internal bool IsDyn => Kind == ProfileType.DynType;

    /// <summary>A list type.</summary>
    /// <param name="element">The element type, or <see langword="null"/> when it is not known.</param>
    internal static StaticType ListOf(StaticType? element) => new(ProfileType.ListType, Element: element);

    /// <summary>A map type.</summary>
    /// <param name="key">The key type, or <see langword="null"/> when it is not known.</param>
    /// <param name="value">The value type, or <see langword="null"/> when it is not known.</param>
    internal static StaticType MapOf(StaticType? key, StaticType? value) => new(ProfileType.MapType, Key: key, Value: value);

    /// <summary>An optional type; its held type is the <see cref="Element"/>.</summary>
    /// <param name="element">The held type, or <see langword="null"/> when it is not known.</param>
    internal static StaticType OptionalOf(StaticType? element) => new(ProfileType.OptionalType, Element: element);

    /// <summary>A type without parameters: a scalar type, <c>null_type</c>, <c>type</c> or <c>dyn</c>.</summary>
    /// <param name="kind">The kind.</param>
    internal static StaticType Of(ProfileType kind) => kind == ProfileType.DynType ? Dyn : new(kind);

    /// <summary>Whether a node of this type may stand where the target type is expected: its kind is the target, or <c>dyn</c>.</summary>
    /// <param name="type">The type, or <see langword="null"/>.</param>
    /// <param name="target">The target type.</param>
    internal static bool Fits(StaticType? type, ProfileType target) =>
        target == ProfileType.DynType || (type is not null && (type.IsDyn || type.Kind == target));

    /// <summary>
    /// Whether two types are assignable to each other under every type that the checkers may give for them: <c>Yes</c> when
    /// they are, <c>No</c> when the known types are not, <c>Maybe</c> when a part is not known. Type values are always
    /// assignable to each other (docs/design/types.md, "Assignability", rule 6).
    /// </summary>
    /// <param name="left">A type.</param>
    /// <param name="right">A type.</param>
    internal static Compatibility Compatible(StaticType? left, StaticType? right)
    {
        if (left is null || right is null)
        {
            return left is { IsDyn: true } || right is { IsDyn: true } ? Compatibility.Yes : Compatibility.Maybe;
        }

        if (left.IsDyn || right.IsDyn)
        {
            return Compatibility.Yes;
        }

        if (left.Kind != right.Kind)
        {
            return Compatibility.No;
        }

        return left.Kind switch
        {
            ProfileType.ListType or ProfileType.OptionalType => Compatible(left.Element, right.Element),
            ProfileType.MapType => Both(Compatible(left.Key, right.Key), Compatible(left.Value, right.Value)),
            _ => Compatibility.Yes,
        };
    }

    /// <summary>
    /// The type kept where two compatible types meet — the result of <c>_?_:_</c> and of <c>_+_</c> on lists, the join of a
    /// literal — as a bound from below: where one is <c>dyn</c>, the other. The checkers give this type or a less specific one.
    /// </summary>
    /// <param name="left">A type.</param>
    /// <param name="right">A type compatible with <paramref name="left"/>.</param>
    internal static StaticType? Meet(StaticType? left, StaticType? right)
    {
        if (left is null || right is null)
        {
            return left is { IsDyn: true } ? right : right is { IsDyn: true } ? left : null;
        }

        if (left.IsDyn)
        {
            return right;
        }

        if (right.IsDyn || left.Kind != right.Kind)
        {
            return left;
        }

        return left.Kind switch
        {
            ProfileType.ListType => ListOf(Meet(left.Element, right.Element)),
            ProfileType.OptionalType => OptionalOf(Meet(left.Element, right.Element)),
            ProfileType.MapType => MapOf(Meet(left.Key, right.Key), Meet(left.Value, right.Value)),
            _ => left,
        };
    }

    /// <summary>
    /// The join of the element types of a literal, from left to right: the meet while each next type is compatible, otherwise
    /// not known, since the checkers may join to <c>dyn</c> or to a more specific type.
    /// </summary>
    /// <param name="soFar">The join of the elements before, or <see langword="null"/> when it is not known.</param>
    /// <param name="next">The type of the next element.</param>
    internal static StaticType? Join(StaticType? soFar, StaticType? next) =>
        Compatible(soFar, next) == Compatibility.Yes ? Meet(soFar, next) : null;

    private static Compatibility Both(Compatibility first, Compatibility second) =>
        first == Compatibility.No || second == Compatibility.No ? Compatibility.No
        : first == Compatibility.Maybe || second == Compatibility.Maybe ? Compatibility.Maybe
        : Compatibility.Yes;
}

/// <summary>Whether two static types are assignable to each other.</summary>
internal enum Compatibility
{
    /// <summary>They are, under every type that the checkers may give.</summary>
    Yes,

    /// <summary>The known types are not.</summary>
    No,

    /// <summary>A part is not known.</summary>
    Maybe,
}

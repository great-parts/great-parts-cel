namespace Great.Parts.Cel.Generator;

// What has(e.f) is for a member ("Presence" of the page of the created code): whether the member is not null. A member of a value
// type that is not Nullable<T> is always present, and its getter is not called.
internal static class PresenceReader
{
    internal const string Always = "true";

    internal static string Read(Shape shape, Operand v) => shape switch
    {
        ScalarShape { Kind: ScalarKind.String or ScalarKind.Bytes or ScalarKind.Object or ScalarKind.CelList or ScalarKind.CelMap } => NotNull(v),
        ScalarShape or EnumShape => Always,
        NullableShape { Underlying: CollectionShape { Family: CollectionFamily.ImmutableArray } } => v.Use() + ".HasValue && !" + v.Use() + ".Value.IsDefault",
        NullableShape => v.Use() + ".HasValue",
        ObjectShape { Kind: ObjectKind.Struct } => Always,
        ObjectShape => NotNull(v),
        CollectionShape { Family: CollectionFamily.ImmutableArray } => "!" + v.Use() + ".IsDefault",
        CollectionShape { IsValueType: true } => Always,
        _ => NotNull(v),
    };

    private static string NotNull(Operand v) => v.Use() + " is not null";
}

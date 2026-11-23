namespace Great.Parts.Cel.Generator;

// How a member that is not a parameter of the constructor is set after the instance exists: by assignment (a public set accessor or a
// mutable field), in the object initializer (a required member, which C# lets nothing else set), or through an accessor method of
// the init accessor (an init member that is not required, which C# lets only an initializer set).
internal enum SetterKind : byte
{
    Assign,
    Initializer,
    Accessor,
}

// The CEL value to .NET conversions that the created code of a literal has. Int and UInt cover the widths up to 64 bits by the keyword
// of the .NET type; Array, List and ImmutableArray are collections of one element shape (List stands for List<T>, IReadOnlyList<T>
// and IList<T>, which the created code fills with a List<T>); Dictionary is Dictionary<K, V> and IReadOnlyDictionary<K, V>.
internal enum CreateKind : byte
{
    Int,
    UInt,
    Double,
    Bool,
    String,
    Guid,
    Char,
    Int128,
    UInt128,
    BigInteger,
    Date,
    TimeOfDay,
    Bytes,
    BytesMemory,
    Timestamp,
    DateTimeOffset,
    DateTime,
    Duration,
    TimeSpan,
    Decimal,
    CelValue,
    CelList,
    CelMap,
    Object,
    Nullable,
    Array,
    List,
    ImmutableArray,
    Dictionary,
}

// A position that a literal creates a value for: ClrName is the type as C# writes it with global:: and the nullable annotation of the
// position; Keyword is the keyword of the integer or floating-point type (long, short, ushort, float, …), and for an object type the
// name without the annotation; Element is the shape of the element, of the value of a dictionary or of the type under a Nullable<T>,
// and Key the shape of the key of a dictionary.
internal sealed record CreateShape(
    CreateKind Kind,
    string ClrName,
    Nullability Nullability,
    string Keyword,
    CreateShape? Element,
    CreateShape? Key,
    bool IsValueObject = false)
{
    // A position whose type takes null: the others get a mismatch for a null value.
    internal bool IsReference => Kind is CreateKind.String or CreateKind.Bytes or CreateKind.CelList or CreateKind.CelMap
        or CreateKind.Array or CreateKind.List or CreateKind.Dictionary || (Kind == CreateKind.Object && !IsValueObject);
}

// One field of the type, in the order of the numbers. Settable is false for a field that no literal can set (Shape is then null).
// A parameter of the constructor has its place (ParameterIndex, the text of the value used when the literal does not set it, empty
// when the parameter has no default); any other field has the kind of its setter and the names it needs.
internal sealed record LiteralFieldModel(
    int Number,
    bool Settable,
    bool Required,
    CreateShape? Shape,
    int ParameterIndex,
    string DefaultText,
    SetterKind Setter,
    string Access,
    string DeclaringType,
    string AccessorName)
{
    internal bool IsParameter => ParameterIndex >= 0;
}

// The plan of the object literals of a type: the constructor the created code calls has ParameterCount parameters, each one the
// field with that ParameterIndex.
internal sealed record LiteralModel(int ParameterCount, bool IsStruct, EquatableArray<LiteralFieldModel> Fields);

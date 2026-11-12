namespace Great.Parts.Cel.Generator;

// Whether a position (a member, an element, a value) takes null: not null by annotation, nullable by annotation, or oblivious
// (no annotation because the nullable context of the project is off), which reads as nullable.
internal enum Nullability : byte
{
    NotNull,
    Nullable,
    Oblivious,
}

// The scalar rows of the input table of the type mapping. Int and UInt cover every integer width; Double covers float and Half.
internal enum ScalarKind : byte
{
    Int,
    UInt,
    Double,
    Bool,
    String,
    Guid,

    // byte[]
    Bytes,

    // ReadOnlyMemory<byte>
    BytesMemory,
    Timestamp,
    DateTimeOffset,
    DateTime,
    Duration,
    TimeSpan,
    Decimal128,
    Decimal,
    CelValue,
    Object,
    JsonElement,
    CelList,
    CelMap,
}

// The collection rows of the type mapping, by the interfaces of the declared type:
//   List: an array, or a type that implements IReadOnlyList<T>.
//   ImmutableArray: System.Collections.Immutable.ImmutableArray<T>, read without a copy and without a box.
//   StructList: a structure other than ImmutableArray<T> that implements IReadOnlyList<T>.
//   ListOnly: a type that implements IList<T> and not IReadOnlyList<T>.
//   Set: IReadOnlySet<T> or ISet<T>, read as a sorted copy.
//   Collection: another IReadOnlyCollection<T>, read as a copy in the order of enumeration.
//   Dictionary: IReadOnlyDictionary<K,V>.
//   DictionaryOnly: IDictionary<K,V> and not IReadOnlyDictionary<K,V>.
internal enum CollectionFamily : byte
{
    List,
    ImmutableArray,
    StructList,
    ListOnly,
    Set,
    Collection,
    Dictionary,
    DictionaryOnly,
}

// How the value of a position is read. ClrName is the type of the position as C# writes it with global:: and with the nullable
// annotation of the position.
internal abstract record Shape(string ClrName, Nullability Nullability);

// Size is the size S of a structure for the cost of its read, set for JsonElement only ("Charges of a read" of the page of the
// created code).
internal sealed record ScalarShape(string ClrName, Nullability Nullability, ScalarKind Kind, int Size = 0) : Shape(ClrName, Nullability);

// An enumeration; CelName is the name of the enumeration type of the context.
internal sealed record EnumShape(string ClrName, Nullability Nullability, string CelName) : Shape(ClrName, Nullability);

// Nullable<T> of a value type; Underlying is the shape of T.
internal sealed record NullableShape(string ClrName, Nullability Nullability, Shape Underlying) : Shape(ClrName, Nullability);

// An object type: a class, a structure or an interface of the context (CelName is its CEL name), or a message class of
// protoc (Kind Message; CelName is then the C# expression of the full name of the message, such as
// global::Acme.Pb.Order.Descriptor.FullName). Size is the size S of a structure for the cost of its read; 0 for the others.
internal sealed record ObjectShape(
    string ClrName,
    Nullability Nullability,
    string CelName,
    ObjectKind Kind,
    bool IsSealed,
    bool IsAbstract,
    int Size = 0) : Shape(ClrName, Nullability);

// A collection. For a dictionary Key is the shape of the key and Element the shape of the value; for the others Key is null.
// IsValueType: the collection is a structure, which has no null, so the created code writes no test for it. Size is the size S of
// a structure that is boxed by the read (a structure other than ImmutableArray<T>); 0 for the others. ReadOnlyCollection: the type
// implements IReadOnlyCollection<T> (it matters for a set, which may implement ISet<T> alone).
internal sealed record CollectionShape(
    string ClrName,
    Nullability Nullability,
    CollectionFamily Family,
    Shape? Key,
    Shape Element,
    bool IsValueType = false,
    bool ReadOnlyCollection = false,
    int Size = 0) : Shape(ClrName, Nullability);

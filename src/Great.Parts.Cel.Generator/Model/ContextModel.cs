namespace Great.Parts.Cel.Generator;

// The model of one type context: everything the writer needs and nothing else, so the writer takes no compilation. All the
// members are strings, numbers, enumerations, other models and EquatableArray, so a model is equal to the model of the
// previous run when nothing it holds changed. The collections inside are sorted: the types by CEL name, the fields by CEL
// field name, both ordinal, so the text does not depend on the order of files, of partial parts or of attributes.
internal sealed record ContextModel(
    string Namespace,
    EquatableArray<ContainingTypeModel> ContainingTypes,
    string Name,
    string SourceName,
    string HintName,
    EquatableArray<ObjectTypeModel> ObjectTypes,
    EquatableArray<EnumModel> Enums);

// A type that contains the context, outermost first. Keyword is the keyword of its declaration: class, struct, interface,
// record or record struct.
internal readonly record struct ContainingTypeModel(string Name, string Keyword);

internal enum ObjectKind : byte
{
    Class,
    Struct,
    Interface,

    // A message class of protoc: not described by the context, the type is named by its descriptor.
    Message,
}

// The description of one object type. ClrName is the type as C# writes it with global:: (no nullable annotation); ClassName
// is the name of the nested description class of the created code.
internal sealed record ObjectTypeModel(
    string CelName,
    string ClrName,
    ObjectKind Kind,
    bool IsSealed,
    bool IsAbstract,
    string ClassName,
    EquatableArray<FieldModel> Fields,
    EquatableArray<ExcludedMemberModel> Excluded);

// A readable member. Number is the position among the sorted fields; Access is the member as C# writes it after the dot (a
// keyword gets @); Shape tells how the value is read and CelType is the type of the field.
internal sealed record FieldModel(int Number, string Name, string Access, Shape Shape, CelTypeModel CelType);

// A member that is left out with a reason: a read of the field is FieldNotAccessible, which gives the reason.
internal sealed record ExcludedMemberModel(string Name, string Reason);

// An enumeration: the constants in declaration order with their values as long (a constant of an enumeration over ulong that
// is above long.MaxValue is left out). FieldName is the name of the static field of the created code.
internal sealed record EnumModel(
    string CelName,
    string ClrName,
    string FieldName,
    string UnderlyingType,
    EquatableArray<EnumConstantModel> Constants);

internal readonly record struct EnumConstantModel(string Name, long Value);

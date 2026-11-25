namespace Great.Parts.Cel.Generator;

// The properties whose type a row of the type mapping turns into a value of CEL without a check of the type at run time: the created
// code writes the factory of CelValue itself instead of the generic CelBindings.Add<T>, with the same handling of null as the row.
// CelValue as a property type needs none: it is bound as it is, and the call of Add<CelValue> is the same.
internal enum DirectKind : byte
{
    None,
    Bool,
    Int,
    UInt,
    Double,
    String,
    Guid,
    CelList,
    CelMap,
}

// One property of an anonymous type: its name and its type as C# writes it with global::. The declaration of the variable and the
// binding of the value are made from them in the created file. Direct is how a value of a row without failure is bound and DirectNullable
// tells that the property is the Nullable<> of it. Collection is the shape of a list or of a dictionary of classes or structures of the
// application, which the created code declares with the CelType of the shape and binds with a view; it is null for any other property.
internal sealed record PropertyModel(
    string Name,
    string CSharpType,
    DirectKind Direct = DirectKind.None,
    bool DirectNullable = false,
    Shape? Collection = null);

// An anonymous type as the created file needs it: the properties in declaration order and the object types and the enumerations (by
// CEL name, ordinal) that the properties reach, which the source of types of the form lists. Two anonymous types of C# are one type
// when the names, the types and the order are the same, which is the equality of this record, so a shape is the key of a file.
internal sealed record AnonymousShape(
    EquatableArray<PropertyModel> Properties,
    EquatableArray<string> Objects,
    EquatableArray<string> Enums);

// The object types and the enumerations that the properties of one anonymous type reach, as the model of a type context writes them
// but with the names of the created classes and fields left empty: the names are chosen for the whole assembly when the closures of all
// calls are put together.
internal sealed record ClassClosure(EquatableArray<ObjectTypeModel> Objects, EquatableArray<EnumModel> Enums)
{
    internal static ClassClosure Empty { get; } = new(default, default);
}

// What the transform returns for one call of CelExpression.Evaluate whose data argument it reads. The shape and the place are
// separate values of the pipeline: the place moves when a file above the call changes, and the shape must stay equal then. The
// diagnostics hold the places they point at. Closure is what the classes in the properties reach, empty for a call without them.
internal sealed record CallSite(AnonymousShape? Shape, LocationInfo Place, EquatableArray<DiagnosticModel> Diagnostics, ClassClosure Closure);

// The shape of a call with the place of the call, to number the shapes in the order of their first call.
internal sealed record ShapeAtPlace(AnonymousShape Shape, LocationInfo Place);

// The closure of a call with the place of the call, to put the closures of all calls together in a fixed order and to place the
// diagnostics of the whole at a call.
internal sealed record ClosureAtPlace(ClassClosure Closure, LocationInfo Place);

// The object types and the enumerations of the whole assembly that the file of descriptions describes, with the final names of the
// created classes and fields, sorted by CEL name.
internal sealed record ClassDescriptionsModel(EquatableArray<ObjectTypeModel> Objects, EquatableArray<EnumModel> Enums);

// The result of putting the closures together: the model of the file, or nothing when there is no class or the limit is exceeded,
// and the diagnostics of the whole. They are separate outputs of the pipeline for the reason that the places of the diagnostics move.
internal sealed record ClassDescriptionsResult(ClassDescriptionsModel? Model, EquatableArray<DiagnosticModel> Diagnostics);

namespace Great.Parts.Cel.Generator;

// One property of an anonymous type: its name and its type as C# writes it with global::. The declaration of the variable and the
// binding of the value are made from these two in the created file, so nothing else is kept.
internal sealed record PropertyModel(string Name, string CSharpType);

// An anonymous type as the created file needs it: the properties in declaration order. Two anonymous types of C# are one type
// when the names, the types and the order are the same, which is the equality of this record, so a shape is the key of a file.
internal sealed record AnonymousShape(EquatableArray<PropertyModel> Properties);

// What the transform returns for one call of CelExpression.Evaluate whose data argument it reads. The shape and the place are
// separate values of the pipeline: the place moves when a file above the call changes, and the shape must stay equal then. The
// diagnostics hold the places they point at.
internal sealed record CallSite(AnonymousShape? Shape, LocationInfo Place, EquatableArray<DiagnosticModel> Diagnostics);

// The shape of a call with the place of the call, to number the shapes in the order of their first call.
internal sealed record ShapeAtPlace(AnonymousShape Shape, LocationInfo Place);

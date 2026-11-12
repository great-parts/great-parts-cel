namespace Great.Parts.Cel.Generator;

// One diagnostic of a context as values: the code, the place and the arguments {0}, {1} … of its message.
internal sealed record DiagnosticModel(string Id, LocationInfo Location, EquatableArray<string> Arguments);

// What the transform returns for one context: the model that the writer takes (nothing when no code is written) and the
// diagnostics. They are separate outputs of the pipeline: the places of the diagnostics move when a file above the context
// changes, and the model must stay equal then.
internal sealed record ContextReadResult(ContextModel? Model, EquatableArray<DiagnosticModel> Diagnostics);

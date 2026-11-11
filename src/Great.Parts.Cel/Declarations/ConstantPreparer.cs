namespace Great.Parts.Cel.Declarations;

// Prepares the constant argument of a call once, when the call has it as a literal: the value of the literal after the escapes of CEL
// are read, and the cache of the environment that the preparer may use. It charges nothing and does not throw for a value.
internal delegate ConstantPreparation ConstantPreparer(string value, object? cache);

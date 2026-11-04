using System;

namespace Great.Parts.Cel.Declarations;

// A declaration whose type comes from a .NET type outside the input rows of the type mapping: a variable
// of AddVariable<T>, or the overload that a delegate form of AddFunction derives. The build reports it as
// TypeNotDeclared; Function is the name of the function of an overload and null for a variable.
internal sealed record UnmappedDeclaration(string Name, string? Function, Type ClrType);

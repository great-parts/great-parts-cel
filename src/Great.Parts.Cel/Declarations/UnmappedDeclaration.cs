using System;

namespace Great.Parts.Cel.Declarations;

// A declaration whose type comes from a .NET type outside the input rows of the type mapping: a variable
// of AddVariable<T>, or the overload that a delegate form of AddFunction derives. The build reports it as
// TypeNotDeclared, unless the .NET type is a class, record, struct or interface of the application that a type source
// describes: the build resolves it by the type index. Function is the name of the function of an overload and null for a
// variable; Delegate is the delegate form of an overload that has a type of the application, and null otherwise.
internal sealed record UnmappedDeclaration(string Name, string? Function, Type ClrType, DelegateDeclaration? Delegate = null);

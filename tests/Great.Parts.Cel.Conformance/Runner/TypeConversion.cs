using System;
using Great.Parts.Cel.Types;
using Type = Cel.Expr.Type;

namespace Great.Parts.Cel.Conformance.Runner;

/// <summary>
/// Converts a type between <see cref="CelType"/> and <c>cel.expr.Type</c> for the kinds the library supports. Other kinds throw
/// <see cref="NotImplementedException"/>.
/// </summary>
internal static class TypeConversion
{
    /// <summary>Converts a type of the library, such as the result type of a program, into a type of a case.</summary>
    /// <param name="type">The type of the library.</param>
    /// <exception cref="NotImplementedException">The library does not support the kind of the type yet.</exception>
    internal static Type ToExprType(CelType type) => throw new NotImplementedException();

    /// <summary>Converts a type of a case, such as the type of a declared variable, into a type of the library.</summary>
    /// <param name="type">The type of the case.</param>
    /// <exception cref="NotImplementedException">The library does not support the kind of the type yet.</exception>
    internal static CelType ToCelType(Type type) => throw new NotImplementedException();
}

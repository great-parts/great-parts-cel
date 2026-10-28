using System;
using Great.Parts.Cel.Values;
using Value = Cel.Expr.Value;

namespace Great.Parts.Cel.Conformance.Runner;

/// <summary>
/// Converts a value between <c>cel.expr.Value</c> and <see cref="CelValue"/> for the kinds the library supports, through the
/// public API of the library only: <c>eng/difftest</c> links this file. Other kinds throw <see cref="NotImplementedException"/>.
/// </summary>
internal static class ValueConversion
{
    /// <summary>Converts a value of a case, such as a binding, into a value of the library.</summary>
    /// <param name="value">The value of the case.</param>
    /// <exception cref="NotImplementedException">The library does not support the kind of the value yet.</exception>
    internal static CelValue ToCelValue(Value value) => throw new NotImplementedException();

    /// <summary>Converts a value of the library, such as the result of an evaluation, into a value of a case.</summary>
    /// <param name="value">The value of the library.</param>
    /// <exception cref="NotImplementedException">The library does not support the kind of the value yet.</exception>
    internal static Value ToExprValue(CelValue value) => throw new NotImplementedException();
}

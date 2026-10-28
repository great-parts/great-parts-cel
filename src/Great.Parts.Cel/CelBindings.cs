using System;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel;

/// <summary>
/// The values of variables for an evaluation: bindings of names to values, independent of any environment.
/// </summary>
/// <remarks>
/// <para>
/// An application loads the data an expression needs and binds it here before the evaluation; evaluation
/// itself is synchronous and does no I/O. Each value becomes a <see cref="CelValue"/> when it is added,
/// without boxing; an object of the application is kept by reference and read lazily during evaluation,
/// not copied. The program finds its variables by name, so names the environment does not declare are
/// ignored, and one set of bindings can serve programs of different environments.
/// </para>
/// <para>
/// A variable without a value is an evaluation error <see cref="CelErrorCode.Unbound"/>, raised only when
/// the expression reads it. A value that does not have the declared type of its variable is the
/// evaluation error <see cref="CelErrorCode.BindingTypeMismatch"/> that names the variable.
/// </para>
/// <para>
/// Bindings are mutable data of a call, not part of an environment. Any number of evaluations can read
/// the same bindings at once, as long as nobody adds to them meanwhile.
/// </para>
/// </remarks>
public sealed class CelBindings
{
    /// <summary>
    /// Creates empty bindings.
    /// </summary>
    public CelBindings()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Binds a name to a value, converted by the static type <typeparamref name="T"/> through the input rows
    /// of the type mapping table of the documentation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The conversion follows the declared type, not the run-time type: a <c>List&lt;int&gt;</c> is a
    /// <c>list(int)</c> read through a view, a <c>Dictionary&lt;string, Order&gt;</c> is a map whose values
    /// are objects described for <c>Order</c>, a <see cref="DateTimeOffset"/> is a <c>timestamp</c>, and a
    /// <see cref="CelValue"/> is taken as it is. Only a value of the static type <see cref="object"/> is
    /// converted by its run-time type, as a <c>dyn</c> value. An object type is found at evaluation in the
    /// type sources of the environment by the declared type of the variable.
    /// </para>
    /// <para>
    /// Values that a row of the table rejects outright are rejected here, before any evaluation: a
    /// <see cref="DateTime"/> whose kind is not <see cref="DateTimeKind.Utc"/>, an undefined
    /// <see cref="System.Text.Json.JsonElement"/>, and on .NET 11 a <c>Decimal128</c> that is a NaN or an
    /// infinity.
    /// </para>
    /// </remarks>
    /// <typeparam name="T">The .NET type of the value, which selects the conversion.</typeparam>
    /// <param name="name">The name of the variable.</param>
    /// <param name="value">The value.</param>
    /// <returns>These bindings.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is empty or already bound, or <paramref name="value"/> is a value the type
    /// mapping table rejects, as listed in the remarks.
    /// </exception>
    public CelBindings Add<T>(string name, T value)
    {
        throw new NotImplementedException();
    }
}

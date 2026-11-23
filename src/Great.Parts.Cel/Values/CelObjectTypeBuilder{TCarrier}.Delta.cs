using System;
using System.Collections.Generic;

namespace Great.Parts.Cel.Values;

public sealed partial class CelObjectTypeBuilder<TCarrier>
{
    /// <summary>
    /// Lets expressions create values of the type with an object literal such as <c>acme.crm.Deal{amount: 10.0}</c>:
    /// <paramref name="create"/> makes the carrier from the fields the literal sets.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Evaluation calls <paramref name="create"/> once for each literal with a new dictionary from the names of the fields
    /// the literal sets to their values; a field that the literal does not set is not in it. The values have the CEL types
    /// declared for the fields. Without this call a literal of the type is a type checking error
    /// <see cref="Diagnostics.CelErrorCode.TypeNotConstructible"/>.
    /// </para>
    /// <para>
    /// An exception thrown by <paramref name="create"/> is an evaluation error
    /// <see cref="Diagnostics.CelErrorCode.ApplicationException"/> that carries it. A <see langword="null"/> carrier is an
    /// evaluation error <see cref="Diagnostics.CelErrorCode.UnexpectedNull"/>. The delegate can be called from any number
    /// of threads at once.
    /// </para>
    /// </remarks>
    /// <param name="create">Creates the carrier of a new value from the fields of a literal.</param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="create"/> is <see langword="null"/>.</exception>
    public CelObjectTypeBuilder<TCarrier> AllowObjectLiterals(Func<IReadOnlyDictionary<string, CelValue>, TCarrier> create)
    {
        ArgumentNullException.ThrowIfNull(create);
        _create = create;
        return this;
    }
}

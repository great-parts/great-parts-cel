using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel.Values;

/// <summary>
/// The outcome of the check of a value against a declared type: the value fits, it does not fit, or the check met an
/// error value as an element, a key or a value of a collection of the application, which is the outcome and is kept.
/// </summary>
internal readonly struct TypeFit
{
    private readonly CelError? _elementError;
    private readonly bool _mismatch;

    private TypeFit(bool mismatch, CelError? elementError)
    {
        _mismatch = mismatch;
        _elementError = elementError;
    }

    /// <summary>The value fits; this is <c>default</c> too, so a check that did not run fits.</summary>
    internal static TypeFit Yes => default;

    /// <summary>The value does not fit the type.</summary>
    internal static TypeFit No => new(mismatch: true, elementError: null);

    /// <summary>Whether the value fits.</summary>
    internal bool IsFit => !_mismatch && _elementError is null;

    /// <summary>The first error value that the check met inside a collection, or <see langword="null"/>.</summary>
    internal CelError? ElementError => _elementError;

    /// <summary>The check met an error value inside a collection: it is not a mismatch of type.</summary>
    /// <param name="error">The error value of the element, key or value.</param>
    /// <returns>The outcome.</returns>
    internal static TypeFit OfElementError(CelError error) => new(mismatch: false, error);
}

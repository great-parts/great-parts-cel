namespace Great.Parts.Cel.Values;

public sealed partial class CelObjectTypeBuilder
{
    /// <summary>
    /// Lets expressions create values of the type with an object literal such as <c>acme.crm.Deal{amount: 10.0}</c>.
    /// </summary>
    /// <remarks>
    /// A literal creates a new read-only dictionary <c>IReadOnlyDictionary&lt;string, object?&gt;</c> with an entry for
    /// every field it sets; the value of an entry is the <see cref="CelValue"/> of the field, which the type reads back
    /// without a conversion. A field that the literal does not set has no entry, so <c>has()</c> gives
    /// <see langword="false"/> for it. Without this call a literal of the type is a type checking error
    /// <see cref="Diagnostics.CelErrorCode.TypeNotConstructible"/>.
    /// </remarks>
    /// <returns>This builder.</returns>
    public CelObjectTypeBuilder AllowObjectLiterals()
    {
        _allowLiterals = true;
        return this;
    }
}

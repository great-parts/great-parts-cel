namespace Great.Parts.Cel.Values;

public sealed partial class CelObjectAttribute
{
    /// <summary>
    /// Whether expressions can create instances of the listed type with an object literal such as
    /// <c>acme.Money{amount: 10.5m, currency: 'RUB'}</c>. The default is <see langword="false"/>: expressions only read
    /// instances that the application passes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A literal runs code of the application, so it is allowed type by type: a type that the generator reaches through
    /// members of a listed type does not get literals. The generated code creates an instance without reflection. It
    /// calls the public parameterless constructor, or the only public constructor whose every parameter has the name of a
    /// field, ignoring case, as a positional record has; then it sets the members with a public <see langword="set"/> or
    /// <see langword="init"/> accessor that the literal names. A member that the literal does not name keeps the value
    /// the constructor and the initializers give it. A <see langword="required"/> member and a constructor parameter
    /// without a default value must be set: otherwise the literal is a type checking error
    /// <see cref="Diagnostics.CelErrorCode.RequiredFieldNotSet"/>. A field that a literal cannot set is a type checking
    /// error <see cref="Diagnostics.CelErrorCode.FieldNotAccessible"/>.
    /// </para>
    /// <para>
    /// An exception thrown by the constructor or by an accessor is an evaluation error
    /// <see cref="Diagnostics.CelErrorCode.ApplicationException"/> that carries it. A type that cannot be created — an
    /// interface, an abstract class, a type without a suitable public constructor — with this property set is a compiler
    /// error of the generator, <c>GPCEL012</c>.
    /// </para>
    /// </remarks>
    public bool AllowObjectLiterals { get; set; }
}

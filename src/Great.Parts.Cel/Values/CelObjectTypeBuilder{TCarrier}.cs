using System;
using System.Collections.Generic;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Values;

/// <summary>
/// Describes an object type at run time, without a .NET type, over a carrier class of the application:
/// each field is read and tested for presence by a delegate over the carrier.
/// </summary>
/// <typeparam name="TCarrier">
/// The class of the application that holds the data of one value, such as a record of an entity whose
/// schema lives in metadata. One carrier class can serve many CEL types.
/// </typeparam>
/// <remarks>
/// <para>
/// Fields are read lazily, when an expression selects them; the data is not copied. A value of the type
/// enters an expression through a variable or field declared with <see cref="CelObjectType.Type"/> of the
/// built description; its instance must be a <typeparamref name="TCarrier"/>, otherwise reading the
/// variable is an error <see cref="CelErrorCode.BindingTypeMismatch"/>.
/// </para>
/// <para>
/// Reading a field that is not present is an error <see cref="CelErrorCode.FieldNotSet"/> that names the
/// type and the field, not a default value: there is no general default for metadata, and a silent zero
/// would hide missing data. An expression tests presence with <c>has()</c> first. A read delegate that
/// returns a value not of the declared type of the field gives an error
/// <see cref="CelErrorCode.FieldTypeMismatch"/>; an exception of a delegate becomes an error
/// <see cref="CelErrorCode.ApplicationException"/>. Object literals of such types are not supported. The rules
/// of reading a field, of presence and of <c>null</c> are on the page
/// <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/runtime-types.md">Types described at run time</see>.
/// </para>
/// <para>
/// The builder is not thread-safe; the descriptions it builds are immutable and can be shared between
/// threads, and the delegates must allow calls from many threads at once.
/// </para>
/// </remarks>
public sealed class CelObjectTypeBuilder<TCarrier>
    where TCarrier : class
{
    private readonly string _name;
    private readonly FieldList _fields = new();
    private readonly List<Func<TCarrier, CelValue>> _reads = [];
    private readonly List<Func<TCarrier, bool>?> _has = [];

    /// <summary>
    /// Starts the description of a type.
    /// </summary>
    /// <param name="name">The full CEL name of the type, identifiers separated by dots, such as <c>acme.crm.Deal</c>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="name"/> is not a full CEL name.</exception>
    public CelObjectTypeBuilder(string name)
    {
        _name = FieldList.CheckTypeName(name);
    }

    /// <summary>
    /// Adds a field. Its number is the number of fields added before it.
    /// </summary>
    /// <param name="name">The field name: an identifier.</param>
    /// <param name="type">The declared CEL type of the field; values that <paramref name="read"/> returns must have this type.</param>
    /// <param name="read">Reads the value of the field from a carrier; called each time an expression selects the field.</param>
    /// <param name="has">
    /// Tells whether the field is present in a carrier, for <c>has()</c> and before each read;
    /// <see langword="null"/> for the default: the field is present when <paramref name="read"/> returns a
    /// value that is not <c>null</c>.
    /// </param>
    /// <returns>This builder.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/>, <paramref name="type"/> or <paramref name="read"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is not an identifier or is the name of a field added before, or
    /// <paramref name="type"/> is a type parameter.
    /// </exception>
    public CelObjectTypeBuilder<TCarrier> AddField(string name, CelType type, Func<TCarrier, CelValue> read, Func<TCarrier, bool>? has = null)
    {
        ArgumentNullException.ThrowIfNull(read);
        _fields.Add(name, type);
        _reads.Add(read);
        _has.Add(has);
        return this;
    }

    /// <summary>
    /// Creates the description from the fields added so far. The builder can be used further; later
    /// fields do not change descriptions built before.
    /// </summary>
    /// <returns>The description, with <see cref="CelObjectType.ClrType"/> <see langword="null"/>.</returns>
    public CelObjectType Build() => new CarrierObjectType(_name, _fields.ToArray(), [.. _reads], [.. _has]);

    // A type described at run time over the carrier class: each field is read and tested by its delegates.
    private sealed class CarrierObjectType(
        string name,
        ReadOnlySpan<CelObjectField> fields,
        Func<TCarrier, CelValue>[] reads,
        Func<TCarrier, bool>?[] has)
        : CelObjectType(name, fields)
    {
        internal override bool Belongs(object instance) => instance is TCarrier;

        internal override bool IsDictionaryCarrier => typeof(TCarrier) == typeof(IReadOnlyDictionary<string, object?>);

        protected internal override CelValue GetField(object instance, int number) =>
            TryReadField(instance, number, out CelValue value) ? value : ObjectRules.FieldNotSet(this, number);

        protected internal override bool HasField(object instance, int number)
        {
            var carrier = (TCarrier)instance;
            return has[number] is Func<TCarrier, bool> present ? present(carrier) : reads[number](carrier).Kind != CelValueKind.Null;
        }

        internal override bool TryReadField(object instance, int number, out CelValue value) =>
            ObjectRules.TryReadCarrierField(this, number, (TCarrier)instance, reads[number], has[number], out value);
    }
}

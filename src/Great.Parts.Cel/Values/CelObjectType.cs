using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
#if NET11_0_OR_GREATER
using System.Numerics;
#endif
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Values;

/// <summary>
/// The description of a CEL object type: its full CEL name, its fields, and how to read a field of an
/// instance, test the presence of a field and compare two instances. Expressions select fields of
/// objects through descriptions; the objects themselves stay the instances of the application.
/// </summary>
/// <remarks>
/// <para>
/// Descriptions come from three sources: the source generator, which describes the .NET types listed in
/// a <see cref="CelTypeContext"/>; <see cref="CelObjectTypeBuilder{TCarrier}"/> and
/// <see cref="CelObjectTypeBuilder"/>, which describe types at run time, without a .NET type; and the
/// package for protobuf messages. An application adds descriptions to an environment through a
/// <see cref="CelTypeProvider"/>. Deriving from this class directly is not a supported way to describe
/// application types: field numbers, presence and equality would have to be written by hand.
/// </para>
/// <para>
/// The protected and abstract members are the contract between the library and the code that derives
/// from this class — the code the source generator creates in the application and the protobuf package.
/// Evaluation calls <see cref="GetField"/> with a field number bound when the program is planned, so
/// reading a field never looks up a name. The field reading members of a derived class do not catch
/// exceptions of application code: evaluation turns them into errors with the code
/// <see cref="CelErrorCode.ApplicationException"/>. A field value that cannot be read is an error value
/// created by <see cref="FieldError"/> or by one of the conversion helpers, not an exception.
/// </para>
/// <para>
/// An instance passed without a description takes it from the declared object type of the variable or
/// field it enters at the top level. Everywhere else — in a <c>dyn</c> place, as an element of a
/// collection, as an argument or a result of a function — it is the description of the exact .NET type
/// of the instance, then of its base classes; interfaces are not considered. A value created by
/// <see cref="CelValue.FromObject(CelObjectType, object)"/> carries its description. A description is
/// immutable, and every member can be called from any number of threads at once.
/// </para>
/// </remarks>
public abstract class CelObjectType
{
    // The fields by name; the names come from the description, never from an expression.
    private readonly FrozenDictionary<string, CelObjectField> _fieldsByName;

    /// <summary>
    /// Initializes a description of a .NET type: a type the source generator describes, or a protobuf
    /// message class.
    /// </summary>
    /// <param name="name">
    /// The full CEL name of the type, such as <c>acme.sales.Order</c>: identifiers separated by dots. It is
    /// the name expressions use and the name by which the environment resolves the type.
    /// </param>
    /// <param name="clrType">
    /// The .NET type of the instances. An instance belongs to the description when it can be cast to this
    /// type: an instance of this type or of a derived type, and for an interface any implementation.
    /// Evaluation finds the description of a value in a <c>dyn</c> place by this type, looking up the
    /// exact .NET type of the instance, then its base classes.
    /// </param>
    /// <param name="fields">
    /// The fields in the order of their numbers: the field at position <c>i</c> has the number <c>i</c>.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="clrType"/> is <see langword="null"/>, or an element of <paramref name="fields"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="name"/> is not a full CEL name; the number of a field differs from its position;
    /// or two fields have the same name.
    /// </exception>
    protected CelObjectType(string name, Type clrType, ReadOnlySpan<CelObjectField> fields)
        : this(name, fields, clrType ?? throw new ArgumentNullException(nameof(clrType)))
    {
    }

    // A description of a type described at run time, without a .NET type: the builders create it.
    private protected CelObjectType(string name, ReadOnlySpan<CelObjectField> fields)
        : this(name, fields, null)
    {
    }

    private CelObjectType(string name, ReadOnlySpan<CelObjectField> fields, Type? clrType)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (!DeclarationNames.IsQualifiedIdentifier(name))
        {
            throw new ArgumentException("The type name '" + name + "' is not a full CEL name.", nameof(name));
        }

        var byName = new Dictionary<string, CelObjectField>(fields.Length, StringComparer.Ordinal);
        for (int number = 0; number < fields.Length; number++)
        {
            CelObjectField field = fields[number];
            ArgumentNullException.ThrowIfNull(field, nameof(fields));
            if (field.Number != number)
            {
                throw new ArgumentException("The number of the field '" + field.Name + "' differs from its position.", nameof(fields));
            }

            if (!byName.TryAdd(field.Name, field))
            {
                throw new ArgumentException("The type '" + name + "' has two fields named '" + field.Name + "'.", nameof(fields));
            }
        }

        Name = name;
        ClrType = clrType;
        Type = CelType.ObjectNamed(name);
        Fields = [.. fields];
        _fieldsByName = byName.ToFrozenDictionary(StringComparer.Ordinal);
    }

    /// <summary>
    /// The full CEL name of the type, for example <c>acme.sales.Order</c> or the full name of a protobuf
    /// message. Expressions refer to the type by this name, error messages show it, and two descriptions
    /// with one name in one environment are a build error <see cref="CelErrorCode.DuplicateType"/>.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The .NET type of the instances, or <see langword="null"/> for a type described at run time.
    /// </summary>
    /// <remarks>
    /// A type described at run time has no .NET type of its own: its values are instances of a carrier
    /// class that many CEL types can share, so such a description is never found by the .NET type of a
    /// value. Its values enter an expression through a variable or field declared with <see cref="Type"/>,
    /// or elsewhere as values created by <see cref="CelValue.FromObject(CelObjectType, object)"/>.
    /// </remarks>
    public Type? ClrType { get; }

    /// <summary>
    /// The CEL type of the instances, an object type with the name <see cref="Name"/>. Use it to declare a
    /// variable, a field of a type described at run time, or an overload parameter of this type.
    /// </summary>
    public CelType Type { get; }

    /// <summary>
    /// The fields expressions can select, in the order of their numbers. Members that the source generator
    /// excluded are not fields.
    /// </summary>
    public ImmutableArray<CelObjectField> Fields { get; }

    /// <summary>
    /// Finds a field by the name an expression uses for it. The comparison is ordinal and case-sensitive.
    /// </summary>
    /// <param name="name">The field name, such as <c>total</c>, or the full name of a protobuf extension field.</param>
    /// <param name="field">The field when the method returns <see langword="true"/>; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when the type has a field with this name.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public bool TryGetField(string name, [NotNullWhen(true)] out CelObjectField? field)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _fieldsByName.TryGetValue(name, out field);
    }

    /// <summary>
    /// Reads the value of a field of an instance; evaluation calls it for a field selection <c>e.f</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The library calls this member only with an instance that belongs to the description and with the number of one
    /// of
    /// <see cref="Fields"/>. An implementation reads the member directly, without reflection and without a name lookup, and converts the value by the input rows of the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>,
    /// using the public factories of <see cref="CelValue"/>, <see cref="CelList"/> and <see cref="CelMap"/> and the
    /// protected conversion helpers of this class.
    /// </para>
    /// <para>
    /// A value that cannot be read — for example a <see langword="null"/> in a member that does not allow
    /// it — is returned as an error value from <see cref="FieldError"/> or from a conversion helper. An
    /// exception thrown by application code, such as a property getter, is not caught here; evaluation
    /// turns it into an error with the code <see cref="CelErrorCode.ApplicationException"/>.
    /// </para>
    /// </remarks>
    /// <param name="instance">An instance that belongs to the description.</param>
    /// <param name="number">The number of the field.</param>
    /// <returns>The value of the field, possibly a value of the kind <see cref="CelValueKind.Error"/>.</returns>
    protected internal abstract CelValue GetField(object instance, int number);

    /// <summary>
    /// Tests whether a field of an instance is present, for the macro <c>has(e.f)</c>.
    /// </summary>
    /// <remarks>
    /// The meaning of presence depends on the source of the type. For a .NET member the field is present
    /// when the member is not <see langword="null"/>, so a member of a value type that is not nullable is
    /// always present: zero and an empty string are ordinary values. For a type described at run time
    /// presence is given by its builder. For a protobuf field it follows the field selection rules of the
    /// CEL language definition. The library calls this member only with an instance that belongs to the
    /// description and with the number of one of <see cref="Fields"/>; exceptions of application code
    /// are not caught here.
    /// </remarks>
    /// <param name="instance">An instance that belongs to the description.</param>
    /// <param name="number">The number of the field.</param>
    /// <returns><see langword="true"/> when the field is present.</returns>
    protected internal abstract bool HasField(object instance, int number);

    /// <summary>
    /// Compares two instances of this type by CEL equality, the <c>==</c> operator of an expression.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The default compares the fields in the order of their numbers: a field present in one instance and
    /// absent in the other makes the instances unequal, and fields present in both compare their values by
    /// <see cref="CelValue.CelEquals"/>. There is no shortcut for the same reference: an instance with a
    /// NaN field is not equal to itself, as the language definition requires. A derived class overrides
    /// the method when its source defines equality differently, as protobuf messages do.
    /// </para>
    /// <para>
    /// The library calls this member only with two instances that belong to the description; instances of
    /// different descriptions are unequal without a call.
    /// </para>
    /// </remarks>
    /// <param name="left">The first instance.</param>
    /// <param name="right">The second instance.</param>
    /// <returns>
    /// A value of the kind <see cref="CelValueKind.Bool"/>, or the first error value met while reading the
    /// fields, of the kind <see cref="CelValueKind.Error"/>.
    /// </returns>
    protected internal virtual CelValue CelEquals(object left, object right) =>
        EqualityRules.ToValue(ObjectRules.InstancesEqual(this, left, right, null, null, out CelValue error), error);

    /// <summary>
    /// Tells why a member that the source generator excluded is not available to expressions.
    /// </summary>
    /// <remarks>
    /// Type checking calls it when an expression selects a name that is not one of <see cref="Fields"/>. A
    /// reason turns the error <see cref="CelErrorCode.NoSuchField"/> into
    /// <see cref="CelErrorCode.FieldNotAccessible"/> with the reason in its message, for example that the
    /// .NET type of the member cannot be read. The default returns <see langword="null"/>; generated code
    /// overrides it for the members it excluded.
    /// </remarks>
    /// <param name="fieldName">The name the expression selects.</param>
    /// <returns>The reason in English, or <see langword="null"/> when no member with this field name was excluded.</returns>
    protected internal virtual string? GetExcludedFieldReason(string fieldName) => null;

    /// <summary>
    /// Whether an expression can create an instance with an object literal such as <c>M{f: 1}</c>.
    /// </summary>
    /// <remarks>
    /// The default is <see langword="false"/>: object literals are supported only for protobuf messages,
    /// and a literal of another type is a type checking error <see cref="CelErrorCode.TypeNotConstructible"/>.
    /// A derived class that returns <see langword="true"/> overrides <see cref="CreateInstance"/>.
    /// </remarks>
    protected internal virtual bool IsConstructible => false;

    /// <summary>
    /// Creates an instance for an object literal from the fields it sets.
    /// </summary>
    /// <remarks>
    /// The library calls this member only when <see cref="IsConstructible"/> is <see langword="true"/>, with
    /// distinct numbers of <see cref="Fields"/> and values whose types type checking has accepted; fields
    /// the literal does not set keep their default. A value that does not fit its field, such as an
    /// <c>int</c> out of the range of a 32-bit field, is reported as an error value from
    /// <see cref="FieldError"/>. The created instance is not changed afterwards.
    /// </remarks>
    /// <param name="fieldNumbers">The numbers of the fields the literal sets, in the order of the literal.</param>
    /// <param name="fieldValues">The values of those fields, in the same order.</param>
    /// <returns>The new instance as an object value, or a value of the kind <see cref="CelValueKind.Error"/>.</returns>
    /// <exception cref="NotSupportedException">The default implementation: the type does not support object literals.</exception>
    protected internal virtual CelValue CreateInstance(ReadOnlySpan<int> fieldNumbers, ReadOnlySpan<CelValue> fieldValues)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Creates an object value that holds <paramref name="instance"/> without its description, for a
    /// field whose declared type is an object type, such as a member of a described .NET class or a
    /// message field.
    /// </summary>
    /// <remarks>
    /// The value allocates no memory beyond the instance itself. In a field declared with an object type
    /// it takes the description of that type; elsewhere its description is found by the exact .NET type
    /// of the instance, then its base classes. To attach a description explicitly, use
    /// <see cref="CelValue.FromObject(CelObjectType, object)"/>.
    /// </remarks>
    /// <param name="instance">The instance; a value of a kind that has its own CEL type, such as a string or a number, must be converted by its factory instead.</param>
    /// <returns>The object value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="instance"/> is <see langword="null"/>; use <see cref="CelValue.Null"/> or <see cref="FieldError"/>.</exception>
    protected static CelValue FromInstance(object instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        return CelValue.FromBareObject(instance);
    }

    /// <summary>
    /// Creates an error value for a field of this type that cannot be read or set. Its text is the template of
    /// the code, nothing more: this type and the field stand in it only where the template names them, and the
    /// position of the field selection names the field.
    /// </summary>
    /// <remarks>
    /// Generated code uses it when a member of a reference type annotated as not nullable is
    /// <see langword="null"/> (<see cref="CelErrorCode.UnexpectedNull"/>), including inside an element
    /// converter of a collection; the protobuf package uses it for a <c>google.protobuf.Any</c> it cannot
    /// unpack (<see cref="CelErrorCode.InvalidAny"/>) and for a literal value out of the range of its field
    /// (<see cref="CelErrorCode.ConversionRange"/>). The conversion helpers of this class create their own
    /// error values. Evaluation adds the position of the field selection.
    /// </remarks>
    /// <param name="number">The number of the field.</param>
    /// <param name="code">An evaluation code, from 3000 to 3999, that tells why the value cannot be read or set.</param>
    /// <param name="detail">What the template of the code needs: the whole text for <see cref="CelErrorCode.ConversionRange"/>, such as <c>300 is out of the range of int</c>; the free part for <see cref="CelErrorCode.InvalidArgument"/>; the .NET type for <see cref="CelErrorCode.TypeNotAccessible"/>; <see langword="null"/> when nothing.</param>
    /// <returns>A value of the kind <see cref="CelValueKind.Error"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="number"/> is not the number of one of <see cref="Fields"/>, or <paramref name="code"/> is not an evaluation code.</exception>
    protected CelValue FieldError(int number, CelErrorCode code, string? detail)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(number);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(number, Fields.Length);
        if (code is < (CelErrorCode)3000 or > (CelErrorCode)3999)
        {
            throw new ArgumentOutOfRangeException(nameof(code), code, "The code is not an evaluation code.");
        }

        return ObjectRules.FieldError(this, number, code, detail);
    }

    /// <summary>
    /// Converts a <see cref="DateTime"/> member to a <c>google.protobuf.Timestamp</c> value.
    /// </summary>
    /// <remarks>
    /// Only a <see cref="DateTime"/> whose <see cref="DateTime.Kind"/> is <see cref="DateTimeKind.Utc"/>
    /// denotes a point in time without the time zone of the process; any other kind gives an error value
    /// with the code <see cref="CelErrorCode.InvalidArgument"/> that names the type and the field. Use
    /// <see cref="CelTimestamp.FromDateTimeOffset"/> for a <see cref="DateTimeOffset"/>, which always converts.
    /// </remarks>
    /// <param name="number">The number of the field, for the error.</param>
    /// <param name="value">The value of the member.</param>
    /// <returns>A value of the kind <see cref="CelValueKind.Timestamp"/> or <see cref="CelValueKind.Error"/>.</returns>
    protected CelValue FromDateTime(int number, DateTime value)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Converts a <see cref="TimeSpan"/> member to a <c>google.protobuf.Duration</c> value, exactly.
    /// </summary>
    /// <remarks>
    /// A duration holds a 64-bit number of nanoseconds, about 292 years each way, while a
    /// <see cref="TimeSpan"/> reaches further; a longer value gives an error value with the code
    /// <see cref="CelErrorCode.ConversionRange"/> that names the type and the field.
    /// </remarks>
    /// <param name="number">The number of the field, for the error.</param>
    /// <param name="value">The value of the member.</param>
    /// <returns>A value of the kind <see cref="CelValueKind.Duration"/> or <see cref="CelValueKind.Error"/>.</returns>
    protected CelValue FromTimeSpan(int number, TimeSpan value)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Converts a member of an enumeration type to an <c>int</c> value, the way CEL reads protobuf
    /// enumerations.
    /// </summary>
    /// <remarks>
    /// Any underlying type converts exactly, except a value of an enumeration over <see cref="ulong"/> that
    /// exceeds <see cref="long.MaxValue"/>: it gives an error value with the code
    /// <see cref="CelErrorCode.ConversionRange"/> and the text <c>18446744073709551615 is out of the range of int</c>, with
    /// the number of the value. The names of the
    /// constants are described by <see cref="CelEnumType"/>, not by the value.
    /// </remarks>
    /// <typeparam name="TEnum">The enumeration type of the member.</typeparam>
    /// <param name="number">The number of the field, for the error.</param>
    /// <param name="value">The value of the member.</param>
    /// <returns>A value of the kind <see cref="CelValueKind.Int"/> or <see cref="CelValueKind.Error"/>.</returns>
    protected CelValue FromEnum<TEnum>(int number, TEnum value)
        where TEnum : struct, Enum
    {
        _ = ConversionRules.TryFromClr(value, out CelValue result);
        return Named(number, result);
    }

    /// <summary>
    /// Converts a member whose CEL type is <c>dyn</c> — a member of the .NET type <see cref="object"/> or
    /// <see cref="System.Text.Json.JsonElement"/> — by the type the value has at run time.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see langword="null"/> becomes <c>null</c>. A value of a .NET type from the input value rows of the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>
    /// — numbers, <see cref="bool"/>, <see cref="string"/>, <see cref="Guid"/>, enumerations, bytes, time types,
    /// <c>decimal</c> types on .NET 11 — becomes a value of its CEL type; a boxed <see cref="CelValue"/> stays as it
    /// is, and an instance of <see cref="CelList"/> or <see cref="CelMap"/> becomes a <c>list</c> or a <c>map</c>
    /// value. A <see cref="System.Text.Json.JsonElement"/> is read by the JSON data conversion of the CEL language
    /// definition, lazily. Any other instance becomes an object value whose description evaluation finds by the exact
    /// .NET type of the instance, then by its base classes; when no type source of the environment describes it, using
    /// the value is an error <see cref="CelErrorCode.TypeNotAccessible"/>.
    /// </para>
    /// <para>
    /// A value that a row of the type mapping rejects gives an error value with the code of that row and its template: <see cref="CelErrorCode.InvalidArgument"/> for a <see cref="DateTime"/> whose kind is not
    /// <see cref="DateTimeKind.Utc"/> or for an undefined <see cref="System.Text.Json.JsonElement"/>,
    /// <see cref="CelErrorCode.ConversionRange"/> for a value out of the range of its CEL type.
    /// </para>
    /// </remarks>
    /// <param name="number">The number of the field, for errors.</param>
    /// <param name="value">The value of the member.</param>
    /// <returns>The value, possibly of the kind <see cref="CelValueKind.Error"/>.</returns>
    protected CelValue FromDynamic(int number, object? value)
    {
        _ = ConversionRules.TryFromClr(value, out CelValue result);
        return Named(number, result);
    }

    /// <summary>
    /// Tells whether an instance belongs to the description: an instance of <see cref="ClrType"/>, or of the carrier
    /// class of a type described at run time.
    /// </summary>
    /// <param name="instance">The instance.</param>
    /// <returns><see langword="true"/> when the instance belongs to the description.</returns>
    internal virtual bool Belongs(object instance) => ClrType!.IsInstanceOfType(instance);

    /// <summary>
    /// Whether the carrier class of a type described at run time is <c>IReadOnlyDictionary&lt;string, object?&gt;</c>, which
    /// decides the code of a .NET collection in a field: a field of a dictionary carrier refuses it with
    /// <c>FieldTypeMismatch</c>.
    /// </summary>
    internal virtual bool IsDictionaryCarrier => false;

    /// <summary>
    /// Reads a field when it is present, for the equality of two instances: by <see cref="HasField"/>, then
    /// <see cref="GetField"/>. The descriptions of types described at run time read their carrier once.
    /// </summary>
    /// <param name="instance">An instance that belongs to the description.</param>
    /// <param name="number">The number of the field.</param>
    /// <param name="value">The value of a present field.</param>
    /// <returns><see langword="true"/> when the field is present.</returns>
    internal virtual bool TryReadField(object instance, int number, out CelValue value)
    {
        if (!HasField(instance, number))
        {
            value = default;
            return false;
        }

        value = GetField(instance, number);
        return true;
    }

    // A value that a row of the type mapping rejects, as an error value that names this type and the field.
    private CelValue Named(int number, CelValue value) =>
        value.TryGetError(out CelError? error) && error.Code is CelErrorCode.ConversionRange or CelErrorCode.InvalidArgument
            ? ObjectRules.FieldError(this, number, error.Code, error.Message)
            : value;

#if NET11_0_OR_GREATER
    /// <summary>
    /// Converts a <see cref="decimal"/> member to a <c>decimal</c> value, exactly: its up to 29 digits and
    /// scale of at most 28 fit the 34 digits of an IEEE 754 decimal128 number. Only on .NET 11.
    /// </summary>
    /// <remarks>
    /// An expression reads such a member only in an environment with the <c>decimal</c> extension;
    /// without it, selecting the field is a type checking error <see cref="CelErrorCode.FieldNotAccessible"/>
    /// that names the extension, and this conversion is not called. There is no conversion to
    /// <c>double</c>.
    /// </remarks>
    /// <param name="value">The value of the member.</param>
    /// <returns>A value of the kind <see cref="CelValueKind.Decimal"/>.</returns>
    protected static CelValue FromDecimal(decimal value)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Converts a <see cref="Decimal128"/> member to a <c>decimal</c> value, exactly, with its digits and
    /// exponent. Only on .NET 11.
    /// </summary>
    /// <remarks>
    /// CEL decimals are finite: a NaN or an infinity gives an error value with the code
    /// <see cref="CelErrorCode.ConversionRange"/> that names the type and the field. An expression reads
    /// such a member only in an environment with the <c>decimal</c> extension.
    /// </remarks>
    /// <param name="number">The number of the field, for the error.</param>
    /// <param name="value">The value of the member.</param>
    /// <returns>A value of the kind <see cref="CelValueKind.Decimal"/> or <see cref="CelValueKind.Error"/>.</returns>
    protected CelValue FromDecimal(int number, Decimal128 value)
    {
        throw new NotImplementedException();
    }
#endif
}

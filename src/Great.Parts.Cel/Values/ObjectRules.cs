using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Values;

/// <summary>
/// The rules of object values: the description of a value, the place rule, whether a value fits an object type, the
/// value of a field read against its declared type, and the default equality of two objects. Errors are values
/// without a place; the node that reads the value gives them its place.
/// </summary>
internal static class ObjectRules
{
    private static readonly Func<CelError, string> NotAccessibleText =
        static error => ".NET type '" + error.First + "' is not available to expressions: no type source of the environment describes it";

    private static readonly Func<CelError, string> FieldNotSetText =
        static error => "field '" + FieldOf(error).Name + "' of type '" + ((CelObjectType)error.First!).Name + "' has no value";

    // The template of the code of the error table and nothing else: the field and the type stand only where the template
    // names them, and the detail fills the free part; the position of the selection names the field.
    private static readonly Func<CelError, string> FieldErrorText = static error => error.Code switch
    {
        CelErrorCode.UnexpectedNull =>
            "member '" + FieldOf(error).Name + "' of type '" + ((CelObjectType)error.First!).Name + "' is null, but its CEL type does not allow null",
        CelErrorCode.InvalidArgument =>
            "invalid argument to '" + ((CelObjectType)error.First!).Name + "." + FieldOf(error).Name + "': " + error.Second,
        CelErrorCode.TypeNotAccessible =>
            ".NET type '" + error.Second + "' is not available to expressions: no type source of the environment describes it",
        _ => error.Second as string ?? string.Empty,
    };

    private static readonly Func<CelError, string> FieldTypeMismatchText =
        static error => "field '" + FieldOf(error).Name + "' of type '" + ((CelObjectType)error.First!).Name + "' has a value of type '"
            + error.Second + "', expected '" + FieldOf(error).Type + "'";

    /// <summary>
    /// The description of an object value: the description of a pair, else the description of the instance in the type
    /// index — of its exact .NET type, then of its nearest base class.
    /// </summary>
    /// <param name="value">A value of the kind <see cref="CelValueKind.Object"/>.</param>
    /// <param name="types">The type index of the environment; <see langword="null"/> outside an evaluation, where a bare instance has no description.</param>
    /// <returns>The description; <see langword="null"/> when the value is not accessible.</returns>
    internal static CelObjectType? Describe(CelValue value, TypeIndex? types)
    {
        if (value.PairedType is CelObjectType paired)
        {
            return paired;
        }

        _ = value.TryGetObject(out object? instance);
        return types?.Describe(instance!);
    }

    /// <summary>
    /// The description of an object value for a node that needs it to work — a selection or presence test by name,
    /// <c>type</c>, the equality of two objects; an inaccessible value is <c>TypeNotAccessible</c>.
    /// </summary>
    /// <param name="value">A value of the kind <see cref="CelValueKind.Object"/>.</param>
    /// <param name="types">The type index of the environment, or <see langword="null"/> outside an evaluation.</param>
    /// <param name="description">The description when the method returns <see langword="true"/>.</param>
    /// <param name="error">The error value <c>TypeNotAccessible</c> when the method returns <see langword="false"/>.</param>
    /// <returns><see langword="true"/> when the value has a description.</returns>
    internal static bool TryDescribe(CelValue value, TypeIndex? types, out CelObjectType? description, out CelValue error)
    {
        description = Describe(value, types);
        if (description is not null)
        {
            error = default;
            return true;
        }

        _ = value.TryGetObject(out object? instance);
        error = NotAccessible(instance!.GetType());
        return false;
    }

    /// <summary>
    /// The error value <c>TypeNotAccessible</c> for a value of a .NET type that no type source describes or that the
    /// <c>object</c> row does not read, without a place.
    /// </summary>
    /// <param name="clrType">The .NET type of the value.</param>
    /// <returns>The error value.</returns>
    internal static CelValue NotAccessible(Type clrType) =>
        CelValue.FromError(new CelError(CelErrorCode.TypeNotAccessible, NotAccessibleText, null, 0, clrType));

    /// <summary>
    /// The name of the type of an object value in the message of a mismatch: the name of its description, or the .NET
    /// type of an instance without one, by <see cref="Type.ToString"/>.
    /// </summary>
    /// <param name="value">A value of the kind <see cref="CelValueKind.Object"/>.</param>
    /// <param name="types">The type index of the environment, or <see langword="null"/>.</param>
    /// <returns>The name.</returns>
    internal static string TypeName(CelValue value, TypeIndex? types)
    {
        if (Describe(value, types) is CelObjectType description)
        {
            return description.Name;
        }

        _ = value.TryGetObject(out object? instance);
        return instance!.GetType().ToString();
    }

    /// <summary>
    /// Tells whether a value fits an object type <c>T</c>: <c>null</c>, or an object value whose description is the
    /// description of <c>T</c> in the type index, the same instance. The check needs no description to exist: an
    /// instance without one does not fit, and nothing here is <c>TypeNotAccessible</c>.
    /// </summary>
    /// <param name="value">A value that is not an error value.</param>
    /// <param name="objectType">An object type.</param>
    /// <param name="types">The type index of the environment; without it no object value fits.</param>
    /// <returns><see langword="true"/> when the value fits.</returns>
    internal static bool Fits(CelValue value, CelType objectType, TypeIndex? types)
    {
        if (value.Kind == CelValueKind.Null)
        {
            return true;
        }

        return value.Kind == CelValueKind.Object
            && types is not null
            && types.TryGetObjectType(objectType.Name, out CelObjectType? declared)
            && ReferenceEquals(Describe(value, types), declared);
    }

    /// <summary>
    /// The place rule, where a value enters from the application at the top level of a place declared with an object
    /// type <c>T</c> — the first read of a variable, the value of a field: a bare instance that belongs to <c>T</c> is the
    /// value of <c>T</c>, paired with <c>T</c> when the type index describes the instance otherwise; then the value must
    /// fit <c>T</c>.
    /// </summary>
    /// <param name="value">The value, not an error value.</param>
    /// <param name="objectType">The declared object type of the place.</param>
    /// <param name="types">The type index of the environment.</param>
    /// <param name="placed">The value of the place: the value, or the pair of a bare instance.</param>
    /// <param name="actualType">The name of the type of the value when it does not fit: its description, or its .NET type.</param>
    /// <returns><see langword="true"/> when the value fits <c>T</c>.</returns>
    internal static bool TryEnterPlace(CelValue value, CelType objectType, TypeIndex types, out CelValue placed, out string? actualType)
    {
        placed = value;
        actualType = null;
        if (value.Kind == CelValueKind.Null)
        {
            return true;
        }

        _ = types.TryGetObjectType(objectType.Name, out CelObjectType? declared);

        // A view that the input rows made from the static type of a binding is its source when the source belongs to the
        // declared type; a view of the application, and a source of another type, stay a list or a map.
        if (declared is not null && RowsSourceOf(value) is { } source && declared.Belongs(source))
        {
            value = CelValue.FromBareObject(source);
            placed = value;
        }

        if (value.Kind != CelValueKind.Object)
        {
            actualType = ValueTypeNames.Of(value.Kind);
            return false;
        }

        CelObjectType? description = Describe(value, types);
        if (declared is not null && ReferenceEquals(description, declared))
        {
            return true;
        }

        _ = value.TryGetObject(out object? instance);
        if (declared is not null && value.PairedType is null && declared.Belongs(instance!))
        {
            placed = CelValue.FromPair(declared, instance!);
            return true;
        }

        actualType = description?.Name ?? instance!.GetType().ToString();
        return false;
    }

    /// <summary>
    /// Reads a field of an instance by its number; for an object-typed field, applies the place rule, and a value that
    /// does not fit the object type is <c>FieldTypeMismatch</c>. A field that is not present is the error value of the
    /// description, <c>FieldNotSet</c> for a type described at run time.
    /// </summary>
    /// <param name="owner">The description of the instance.</param>
    /// <param name="instance">An instance that belongs to the description.</param>
    /// <param name="number">The number of one of the fields.</param>
    /// <param name="types">The type index of the environment, or <see langword="null"/> outside an evaluation, where no place rule applies.</param>
    /// <returns>The value of the field, possibly an error value.</returns>
    internal static CelValue ReadField(CelObjectType owner, object instance, int number, TypeIndex? types) =>
        Place(owner, number, owner.GetField(instance, number), types);

    /// <summary>
    /// Steps 1 and 3 of reading a field of a type described at run time over a carrier with a read delegate: presence by
    /// the presence delegate, or else by a value that is not <c>null</c>, taken once; then the value against the declared
    /// type.
    /// </summary>
    /// <typeparam name="TCarrier">The carrier class.</typeparam>
    /// <param name="owner">The description.</param>
    /// <param name="number">The number of the field.</param>
    /// <param name="carrier">The carrier.</param>
    /// <param name="read">The read delegate of the field.</param>
    /// <param name="has">The presence delegate of the field, or <see langword="null"/>.</param>
    /// <param name="value">The value of a present field, or an error value.</param>
    /// <returns><see langword="true"/> when the field is present.</returns>
    internal static bool TryReadCarrierField<TCarrier>(
        CelObjectType owner,
        int number,
        TCarrier carrier,
        Func<TCarrier, CelValue> read,
        Func<TCarrier, bool>? has,
        out CelValue value)
    {
        if (has is not null && !has(carrier))
        {
            value = default;
            return false;
        }

        value = read(carrier);
        if (has is null && value.Kind == CelValueKind.Null)
        {
            return false;
        }

        value = TopLevelField(owner, number, value);
        return true;
    }

    /// <summary>
    /// Steps 1 to 3 of reading a field of a type described over a dictionary carrier: the entry with the field name as
    /// its key; a dictionary of the same carrier in an object-typed field is a bare carrier of that type; any other entry
    /// converts as a value of the .NET type <see cref="object"/>, except a .NET collection, which is
    /// <c>FieldTypeMismatch</c>.
    /// </summary>
    /// <param name="owner">The description.</param>
    /// <param name="number">The number of the field.</param>
    /// <param name="carrier">The dictionary.</param>
    /// <param name="value">The value of a present field, or an error value.</param>
    /// <returns><see langword="true"/> when the field is present.</returns>
    internal static bool TryReadDictionaryField(CelObjectType owner, int number, IReadOnlyDictionary<string, object?> carrier, out CelValue value)
    {
        CelObjectField field = owner.Fields[number];
        if (!carrier.TryGetValue(field.Name, out object? entry))
        {
            value = default;
            return false;
        }

        if (field.Type.Kind == CelTypeKind.Object && entry is IReadOnlyDictionary<string, object?> nested)
        {
            value = CelValue.FromBareObject(nested);
        }
        else
        {
            _ = ConversionRules.TryFromClr(entry, out value);
            value = TopLevelField(owner, number, value);
        }

        return true;
    }

    /// <summary>
    /// The error value of a field of a description that cannot be read or set, without a place.
    /// </summary>
    /// <param name="owner">The description.</param>
    /// <param name="number">The number of the field.</param>
    /// <param name="code">An evaluation code.</param>
    /// <param name="detail">What the code needs besides the names of the type and the field, or <see langword="null"/>.</param>
    /// <returns>The error value.</returns>
    internal static CelValue FieldError(CelObjectType owner, int number, CelErrorCode code, string? detail) =>
        CelValue.FromError(new CelError(code, FieldErrorText, null, 0, owner, detail, number));

    /// <summary>
    /// The error value <c>FieldNotSet</c> of a field, without a place.
    /// </summary>
    /// <param name="owner">The description.</param>
    /// <param name="number">The number of the field.</param>
    /// <returns>The error value.</returns>
    internal static CelValue FieldNotSet(CelObjectType owner, int number) =>
        CelValue.FromError(new CelError(CelErrorCode.FieldNotSet, FieldNotSetText, null, 0, owner, number: number));

    /// <summary>
    /// The default equality of two instances of one description: the fields in the order of their numbers, charging 1,
    /// then for each field the reads of both sides before the comparison of the values. A field present in one instance
    /// only makes them unequal; a field absent from both is skipped. Each read is an event of the evaluation, and an
    /// exception of application code in it is <c>ApplicationException</c> at the node of the operation.
    /// </summary>
    /// <param name="owner">The description of both instances.</param>
    /// <param name="left">The left instance.</param>
    /// <param name="right">The right instance.</param>
    /// <param name="types">The type index of the environment, or <see langword="null"/>.</param>
    /// <param name="meter">The meter of an evaluation with a cost limit; <see langword="null"/> charges nothing.</param>
    /// <param name="error">The error value when the result is <see cref="ValueEquality.Error"/>.</param>
    /// <param name="boundary">The exception boundary and the event counter of an evaluation; <see langword="null"/> outside one, where an exception passes.</param>
    /// <returns>The result.</returns>
    internal static ValueEquality InstancesEqual(
        CelObjectType owner,
        object left,
        object right,
        TypeIndex? types,
        IEvaluationMeter? meter,
        out CelValue error,
        IApplicationBoundary? boundary = null)
    {
        error = default;
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            return ValueEquality.InsufficientStack;
        }

        meter?.Charge(1);
        int count = owner.Fields.Length;
        for (int number = 0; number < count; number++)
        {
            meter?.Charge(2);
            if (!TryRead(owner, left, number, boundary, out bool leftHas, out CelValue leftValue, out error)
                || !TryRead(owner, right, number, boundary, out bool rightHas, out CelValue rightValue, out error))
            {
                return ValueEquality.Error;
            }

            if (leftHas != rightHas)
            {
                return ValueEquality.NotEqual;
            }

            if (!leftHas)
            {
                continue;
            }

            ValueEquality field = EqualityRules.AreEqual(
                Place(owner, number, leftValue, types),
                Place(owner, number, rightValue, types),
                meter,
                out error,
                types,
                boundary);
            if (field != ValueEquality.Equal)
            {
                return field;
            }
        }

        return ValueEquality.Equal;
    }

    /// <summary>
    /// The equality of two object values: each gets its description, an inaccessible one is the result; two different
    /// descriptions are unequal without reading fields; one description compares the fields.
    /// </summary>
    /// <param name="left">A value of the kind <see cref="CelValueKind.Object"/>.</param>
    /// <param name="right">A value of the kind <see cref="CelValueKind.Object"/>.</param>
    /// <param name="types">The type index of the environment, or <see langword="null"/> outside an evaluation.</param>
    /// <param name="meter">The meter of an evaluation with a cost limit; <see langword="null"/> charges nothing.</param>
    /// <param name="error">The error value when the result is <see cref="ValueEquality.Error"/>.</param>
    /// <param name="boundary">The exception boundary and the event counter of an evaluation; <see langword="null"/> outside one.</param>
    /// <returns>The result.</returns>
    internal static ValueEquality AreEqual(
        CelValue left,
        CelValue right,
        TypeIndex? types,
        IEvaluationMeter? meter,
        out CelValue error,
        IApplicationBoundary? boundary = null)
    {
        if (!TryDescribe(left, types, out CelObjectType? leftType, out error) || !TryDescribe(right, types, out CelObjectType? rightType, out error))
        {
            return ValueEquality.Error;
        }

        if (!ReferenceEquals(leftType, rightType))
        {
            meter?.Charge(1);
            return ValueEquality.NotEqual;
        }

        _ = left.TryGetObject(out object? leftInstance);
        _ = right.TryGetObject(out object? rightInstance);
        return InstancesEqual(leftType!, leftInstance!, rightInstance!, types, meter, out error, boundary);
    }

    // A read of a field for the equality: an event, then the read inside the exception boundary of an evaluation. False
    // when application code threw; the error is then ApplicationException.
    private static bool TryRead(
        CelObjectType owner,
        object instance,
        int number,
        IApplicationBoundary? boundary,
        out bool present,
        out CelValue value,
        out CelValue error)
    {
        error = default;
        boundary?.Event();
        try
        {
            present = owner.TryReadField(instance, number, out value);
            return true;
        }
        catch (Exception exception) when (boundary is not null && boundary.IsApplicationException(exception))
        {
            present = false;
            value = default;
            error = boundary.FieldException(exception, owner, number);
            return false;
        }
    }

    // The place rule for the value of an object-typed field, with the type index; a value that does not fit is
    // FieldTypeMismatch. Other fields, error values and reads without the index keep the value.
    private static CelValue Place(CelObjectType owner, int number, CelValue value, TypeIndex? types)
    {
        CelType declaredType = owner.Fields[number].Type;
        if (value.Kind == CelValueKind.Object && RejectsCollection(owner, number, value, types) is { } rejection)
        {
            return rejection;
        }

        if (types is null || declaredType.Kind != CelTypeKind.Object || value.Kind != CelValueKind.Object)
        {
            return value;
        }

        return TryEnterPlace(value, declaredType, types, out CelValue placed, out string? actualType)
            ? placed
            : FieldTypeMismatch(owner, number, actualType!);
    }

    // The value of a field against its declared type at the top level, without descending into a list or a map: the
    // kind of a primitive, list(…), map(…) and type(…); null or the primitive for wrapper(P); null or an object value
    // for an object type, whose description the reading node checks by the place rule (ReadField); anything for dyn.
    // An error value is the value of the field. Without the type index, a bare instance in a field that takes no
    // object is named by its .NET type.
    private static CelValue TopLevelField(CelObjectType owner, int number, CelValue value)
    {
        CelType declaredType = owner.Fields[number].Type;
        if (value.Kind == CelValueKind.Error)
        {
            return value;
        }

        bool fits = declaredType.Kind == CelTypeKind.Object
            ? value.Kind is CelValueKind.Null or CelValueKind.Object
            : ValueTypeRules.FitsTopLevel(value, declaredType);
        return fits
            ? value
            : FieldTypeMismatch(owner, number, value.Kind == CelValueKind.Object ? TypeName(value, null) : ValueTypeNames.Of(value.Kind));
    }

    // The source of a lazy view that the input rows made, which the place rule looks through; null for any other value.
    private static object? RowsSourceOf(CelValue value) =>
        value.TryGetList(out CelList? list) ? list.RowsSource : value.TryGetMap(out CelMap? map) ? map.RowsSource : null;

    /// <summary>
    /// The steps 2 and 3 of reading a value of the application by a node that has the type index — the identifier, the
    /// selection, the call: a bare instance of the collection feature — <c>System.Collections.IEnumerable</c> except
    /// <c>string</c>, <c>byte[]</c>, <c>CelList</c> and <c>CelMap</c> — that the index describes, by its type or a base class,
    /// is an object even if it is enumerable; one that it does not describe is <c>TypeNotAccessible</c>. Step 1, a place
    /// declared with an object type that the instance belongs to, comes first, and any other value, a bare instance that is
    /// not a collection included, is not decided here.
    /// </summary>
    /// <param name="value">The value as the conversion gave it.</param>
    /// <param name="declaredType">The declared type of the place, or <see langword="null"/> without one.</param>
    /// <param name="types">The type index of the environment.</param>
    /// <param name="error">The error value, without a place, when the instance is a collection that nothing describes.</param>
    /// <returns><see langword="true"/> when the value is that error.</returns>
    internal static bool TryRejectCollection(CelValue value, CelType? declaredType, TypeIndex types, out CelValue error)
    {
        error = default;
        if (value.PairedType is not null || !value.TryGetObject(out object? instance) || !ConversionRules.IsClrCollection(instance))
        {
            return false;
        }

        if (declaredType is { Kind: CelTypeKind.Object } && types.TryGetObjectType(declaredType.Name, out CelObjectType? declared) && declared.Belongs(instance))
        {
            return false;
        }

        if (types.Describe(instance) is not null)
        {
            return false;
        }

        error = NotAccessible(instance.GetType());
        return true;
    }

    // The same for the value of a field of an object: in a field of a dictionary carrier the refusal is FieldTypeMismatch
    // with the .NET type, in any other field TypeNotAccessible; null when the value is not refused.
    private static CelValue? RejectsCollection(CelObjectType owner, int number, CelValue value, TypeIndex? types)
    {
        if (value.PairedType is not null || !value.TryGetObject(out object? instance) || !ConversionRules.IsClrCollection(instance))
        {
            return null;
        }

        CelType declaredType = owner.Fields[number].Type;
        if (types is not null
            && ((declaredType.Kind == CelTypeKind.Object && types.TryGetObjectType(declaredType.Name, out CelObjectType? declared) && declared.Belongs(instance))
                || types.Describe(instance) is not null))
        {
            return null;
        }

        return owner.IsDictionaryCarrier ? FieldTypeMismatch(owner, number, instance.GetType()) : NotAccessible(instance.GetType());
    }

    // FieldTypeMismatch without a place; the actual type is a name or a .NET type, written when the message is read.
    private static CelValue FieldTypeMismatch(CelObjectType owner, int number, object actualType) =>
        CelValue.FromError(new CelError(CelErrorCode.FieldTypeMismatch, FieldTypeMismatchText, null, 0, owner, actualType, number));

    private static CelObjectField FieldOf(CelError error) => ((CelObjectType)error.First!).Fields[(int)error.Number];
}

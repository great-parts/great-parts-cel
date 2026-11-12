using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if NET11_0_OR_GREATER
using System.Numerics;
#endif
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Values;

/// <summary>
/// A CEL value: what an expression computes and what function implementations of the application
/// receive and return.
/// </summary>
/// <remarks>
/// <para>
/// Numbers, booleans, <c>null</c>, timestamps, durations and decimals are stored inside the structure
/// without allocation. Strings, bytes, lists, maps, types and objects are references; an object of the
/// application is the instance itself, not a copy. The size and layout of the structure are not part of
/// the public contract.
/// </para>
/// <para>
/// <see cref="Kind"/> tells the kind of the value, and the <c>TryGet…</c> method of that kind reads it.
/// <c>default(CelValue)</c> is <c>null</c>.
/// </para>
/// <para>
/// .NET equality (<see cref="Equals(CelValue)"/>, <see cref="operator ==(CelValue, CelValue)"/>) and CEL equality
/// (<see cref="CelEquals"/>) are different operations: .NET equality is reflexive, as
/// <see cref="object.Equals(object)"/> requires, while in CEL a NaN is not equal to itself and
/// <c>1 == 1u</c>.
/// </para>
/// <para>
/// The structure is immutable and can be shared between threads. A value that refers to application
/// data — an object, or a list or map over a collection of the application — reads that data when it is
/// accessed; if the application changes the data meanwhile, the result is undefined.
/// </para>
/// </remarks>
public readonly partial struct CelValue : IEquatable<CelValue>
{
    // The layout with an explicit kind, 32 bytes: a reference, 16 bytes of payload and the kind. Numbers and bool
    // live in _p0; a string is _ref, and _p0 of a string that a JSON element gave to a binding holds the units of its read;
    // bytes are the array or the memory manager in _ref, the offset in _p0 and the length in _p1. default(CelValue) is null.
    private readonly object? _ref;
    private readonly ulong _p0;
    private readonly ulong _p1;
    private readonly CelValueKind _kind;

    private CelValue(CelValueKind kind, object? reference, ulong p0, ulong p1)
    {
        _kind = kind;
        _ref = reference;
        _p0 = p0;
        _p1 = p1;
    }

    /// <summary>
    /// The CEL type the value has at evaluation, and so the <c>TryGet…</c> method that reads it: for
    /// example, <see cref="CelValueKind.Int"/> is read with <see cref="TryGetInt64(out long)"/>;
    /// <see cref="CelValueKind"/> names the method of each kind.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The kind is the runtime type, not the declared one: a value of a wrapper type is a value of the
    /// wrapped primitive type or <see cref="CelValueKind.Null"/>, a <c>google.protobuf.Any</c> value is the
    /// unpacked message, an enumeration value is <see cref="CelValueKind.Int"/>, and a value of a
    /// <c>dyn</c> declaration has the kind of whatever it holds.
    /// </para>
    /// <para>
    /// <see cref="CelValueKind.Error"/> means the value is an error inside evaluation, such as an element
    /// of a list over application data that could not be read; read it with
    /// <see cref="TryGetError(out CelError)"/>. A function implementation can meet such a value; the
    /// result of an evaluation never has this kind, because an error that reaches the result becomes the
    /// error of the evaluation result.
    /// </para>
    /// <para>
    /// Code compiled for .NET 10 can meet a kind it does not know, <c>Decimal</c>, when it runs on
    /// .NET 11; a <see langword="switch"/> over the kind needs a default branch. The same holds for
    /// <see cref="CelValueKind.Abstract"/>, the kind of a value of an abstract type of an extension set.
    /// </para>
    /// </remarks>
    public CelValueKind Kind => _kind;

    /// <summary>
    /// The CEL <c>null</c>, the same as <c>default(CelValue)</c>.
    /// </summary>
    public static CelValue Null => default;

    /// <summary>
    /// Creates a <c>bool</c> value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The CEL value; no memory is allocated.</returns>
    public static CelValue FromBoolean(bool value)
    {
        return new CelValue(CelValueKind.Bool, null, value ? 1UL : 0UL, 0);
    }

    /// <summary>
    /// Creates an <c>int</c> value, a signed 64-bit integer.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The CEL value; no memory is allocated.</returns>
    public static CelValue FromInt64(long value)
    {
        return new CelValue(CelValueKind.Int, null, unchecked((ulong)value), 0);
    }

    /// <summary>
    /// Creates a <c>uint</c> value, an unsigned 64-bit integer.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The CEL value; no memory is allocated.</returns>
    public static CelValue FromUInt64(ulong value)
    {
        return new CelValue(CelValueKind.UInt, null, value, 0);
    }

    /// <summary>
    /// Creates a <c>double</c> value. NaN and infinities are valid <c>double</c> values in CEL.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The CEL value; no memory is allocated.</returns>
    public static CelValue FromDouble(double value)
    {
        return new CelValue(CelValueKind.Double, null, BitConverter.DoubleToUInt64Bits(value), 0);
    }

    /// <summary>
    /// Creates a <c>string</c> value that refers to <paramref name="value"/> without copying it.
    /// CEL functions on strings count Unicode code points. The application is responsible for valid
    /// text: a string with a lone surrogate is not a valid CEL string.
    /// </summary>
    /// <param name="value">The string.</param>
    /// <returns>The CEL value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>; use <see cref="Null"/> for <c>null</c>.</exception>
    public static CelValue FromString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new CelValue(CelValueKind.String, value, 0, 0);
    }

    /// <summary>
    /// Creates the <c>string</c> value of a JSON string that a binding holds: the units that each read of the identifier
    /// charges ride in the unused payload, because the length of the raw token is not known once the string is created.
    /// </summary>
    /// <param name="value">The string.</param>
    /// <param name="readUnits">The units of a read, 1 + ⌈L/10⌉.</param>
    /// <returns>The CEL value.</returns>
    internal static CelValue FromJsonString(string value, ulong readUnits) => new(CelValueKind.String, value, readUnits, 0);

    /// <summary>
    /// The units that a read of this value charges beyond the unit of the node: those of a JSON string that a binding holds,
    /// and 0 for any other value.
    /// </summary>
    internal ulong ReadUnits => _kind == CelValueKind.String ? _p0 : 0;

    /// <summary>
    /// Creates a <c>bytes</c> value that refers to <paramref name="value"/> without copying it. The
    /// application must not change the bytes while the value is in use.
    /// </summary>
    /// <param name="value">The bytes.</param>
    /// <returns>The CEL value.</returns>
    public static CelValue FromBytes(ReadOnlyMemory<byte> value)
    {
        if (MemoryMarshal.TryGetMemoryManager(value, out MemoryManager<byte>? manager, out int start, out int length))
        {
            return new CelValue(CelValueKind.Bytes, manager, (ulong)start, (ulong)length);
        }

        // Otherwise the memory is over an array, or empty.
        _ = MemoryMarshal.TryGetArray(value, out ArraySegment<byte> segment);
        return new CelValue(CelValueKind.Bytes, segment.Array, (ulong)segment.Offset, (ulong)segment.Count);
    }

    /// <summary>
    /// Creates a <c>google.protobuf.Timestamp</c> value.
    /// </summary>
    /// <param name="value">The point in time.</param>
    /// <returns>The CEL value; no memory is allocated.</returns>
    public static CelValue FromTimestamp(CelTimestamp value)
    {
        return new CelValue(CelValueKind.Timestamp, null, unchecked((ulong)value.Seconds), (ulong)value.Nanoseconds);
    }

    /// <summary>
    /// Creates a <c>google.protobuf.Duration</c> value.
    /// </summary>
    /// <param name="value">The time span.</param>
    /// <returns>The CEL value; no memory is allocated.</returns>
    public static CelValue FromDuration(CelDuration value)
    {
        return new CelValue(CelValueKind.Duration, null, unchecked((ulong)value.TotalNanoseconds), 0);
    }

    /// <summary>
    /// Creates a <c>list</c> value that refers to <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The list.</param>
    /// <returns>The CEL value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public static CelValue FromList(CelList value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new CelValue(CelValueKind.List, value, 0, 0);
    }

    /// <summary>
    /// Creates a <c>map</c> value that refers to <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The map.</param>
    /// <returns>The CEL value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public static CelValue FromMap(CelMap value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new CelValue(CelValueKind.Map, value, 0, 0);
    }

    /// <summary>
    /// Creates a type value, such as the value of the identifier <c>int</c> in an expression. Type values
    /// have their parameters erased: the value for <c>list(int)</c> equals the value for <c>list</c>.
    /// </summary>
    /// <param name="value">The type.</param>
    /// <returns>The CEL value.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is a type parameter, which has no value.</exception>
    public static CelValue FromType(CelType value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Kind == CelTypeKind.TypeParameter)
        {
            throw new ArgumentException($"The type parameter '{value}' has no type value.", nameof(value));
        }

        return new CelValue(CelValueKind.Type, value, 0, 0);
    }

    /// <summary>
    /// Creates an <c>optional_type(T)</c> value: <c>optional.none()</c> for an empty
    /// <paramref name="value"/>, otherwise <c>optional.of(x)</c>, where <c>x</c> can be <c>null</c>.
    /// </summary>
    /// <param name="value">The optional value.</param>
    /// <returns>The CEL value.</returns>
    /// <exception cref="ArgumentException">The optional holds a value of the kind <see cref="CelValueKind.Error"/>.</exception>
    public static CelValue FromOptional(CelOptional<CelValue> value)
    {
        if (!value.HasValue)
        {
            return EmptyOptional;
        }

        if (value.Value.Kind == CelValueKind.Error)
        {
            throw new ArgumentException("An optional cannot hold an error value.", nameof(value));
        }

        return OptionalOfValue(value.Value);
    }

    // The one empty optional: the kind and no reference. optional.none() returns it, and nothing allocates.
    internal static CelValue EmptyOptional => new(CelValueKind.Optional, null, 0, 0);

    // The optional that holds the value, which is not an error value: one box.
    internal static CelValue OptionalOfValue(CelValue held) => new(CelValueKind.Optional, new OptionalBox(held), 0, 0);

#if NET11_0_OR_GREATER
    /// <summary>
    /// Creates a <c>decimal</c> value. The value keeps its digits and exponent, so <c>1.50</c> and
    /// <c>1.5</c> are equal but convert to different strings, and <c>-0</c> stays negative zero.
    /// Only on .NET 11; expressions use the value only in environments with the <c>decimal</c> extension.
    /// </summary>
    /// <param name="value">The value; only finite values are valid CEL decimals.</param>
    /// <returns>The CEL value; no memory is allocated.</returns>
    /// <exception cref="ArgumentException"><paramref name="value"/> is a NaN or an infinity.</exception>
    public static CelValue FromDecimal(Decimal128 value)
    {
        throw new NotImplementedException();
    }
#endif

    /// <summary>
    /// Reads a <c>bool</c> value.
    /// </summary>
    /// <param name="value">The value when the method returns <see langword="true"/>; otherwise <see langword="false"/>.</param>
    /// <returns><see langword="true"/> when <see cref="Kind"/> is <see cref="CelValueKind.Bool"/>.</returns>
    public bool TryGetBoolean(out bool value)
    {
        value = _kind == CelValueKind.Bool && _p0 != 0;
        return _kind == CelValueKind.Bool;
    }

    /// <summary>
    /// Reads an <c>int</c> value. A <c>uint</c> or a <c>double</c> is not converted.
    /// </summary>
    /// <param name="value">The value when the method returns <see langword="true"/>; otherwise 0.</param>
    /// <returns><see langword="true"/> when <see cref="Kind"/> is <see cref="CelValueKind.Int"/>.</returns>
    public bool TryGetInt64(out long value)
    {
        value = _kind == CelValueKind.Int ? unchecked((long)_p0) : 0;
        return _kind == CelValueKind.Int;
    }

    /// <summary>
    /// Reads a <c>uint</c> value. An <c>int</c> or a <c>double</c> is not converted.
    /// </summary>
    /// <param name="value">The value when the method returns <see langword="true"/>; otherwise 0.</param>
    /// <returns><see langword="true"/> when <see cref="Kind"/> is <see cref="CelValueKind.UInt"/>.</returns>
    public bool TryGetUInt64(out ulong value)
    {
        value = _kind == CelValueKind.UInt ? _p0 : 0;
        return _kind == CelValueKind.UInt;
    }

    /// <summary>
    /// Reads a <c>double</c> value. An <c>int</c> or a <c>uint</c> is not converted.
    /// </summary>
    /// <param name="value">The value when the method returns <see langword="true"/>; otherwise 0.</param>
    /// <returns><see langword="true"/> when <see cref="Kind"/> is <see cref="CelValueKind.Double"/>.</returns>
    public bool TryGetDouble(out double value)
    {
        value = _kind == CelValueKind.Double ? BitConverter.UInt64BitsToDouble(_p0) : 0;
        return _kind == CelValueKind.Double;
    }

    /// <summary>
    /// Reads a <c>string</c> value.
    /// </summary>
    /// <param name="value">The string when the method returns <see langword="true"/>; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when <see cref="Kind"/> is <see cref="CelValueKind.String"/>.</returns>
    public bool TryGetString([NotNullWhen(true)] out string? value)
    {
        value = _kind == CelValueKind.String ? (string?)_ref : null;
        return value is not null;
    }

    /// <summary>
    /// Reads a <c>bytes</c> value without copying it. The caller must not change the bytes.
    /// </summary>
    /// <param name="value">The bytes when the method returns <see langword="true"/>; otherwise empty.</param>
    /// <returns><see langword="true"/> when <see cref="Kind"/> is <see cref="CelValueKind.Bytes"/>.</returns>
    public bool TryGetBytes(out ReadOnlyMemory<byte> value)
    {
        if (_kind != CelValueKind.Bytes)
        {
            value = default;
            return false;
        }

        value = _ref is MemoryManager<byte> manager
            ? manager.Memory.Slice((int)_p0, (int)_p1)
            : new ReadOnlyMemory<byte>((byte[]?)_ref, (int)_p0, (int)_p1);
        return true;
    }

    /// <summary>
    /// Reads a <c>google.protobuf.Timestamp</c> value.
    /// </summary>
    /// <param name="value">The point in time when the method returns <see langword="true"/>; otherwise the default value.</param>
    /// <returns><see langword="true"/> when <see cref="Kind"/> is <see cref="CelValueKind.Timestamp"/>.</returns>
    public bool TryGetTimestamp(out CelTimestamp value)
    {
        // The payload was checked when the value was made.
        value = _kind == CelValueKind.Timestamp ? CelTimestamp.FromChecked(unchecked((long)_p0), (int)_p1) : default;
        return _kind == CelValueKind.Timestamp;
    }

    /// <summary>
    /// Reads a <c>google.protobuf.Duration</c> value.
    /// </summary>
    /// <param name="value">The time span when the method returns <see langword="true"/>; otherwise zero.</param>
    /// <returns><see langword="true"/> when <see cref="Kind"/> is <see cref="CelValueKind.Duration"/>.</returns>
    public bool TryGetDuration(out CelDuration value)
    {
        value = _kind == CelValueKind.Duration ? new CelDuration(unchecked((long)_p0)) : default;
        return _kind == CelValueKind.Duration;
    }

    /// <summary>
    /// Reads a <c>list</c> value.
    /// </summary>
    /// <param name="value">The list when the method returns <see langword="true"/>; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when <see cref="Kind"/> is <see cref="CelValueKind.List"/>.</returns>
    public bool TryGetList([NotNullWhen(true)] out CelList? value)
    {
        value = _kind == CelValueKind.List ? (CelList?)_ref : null;
        return value is not null;
    }

    /// <summary>
    /// Reads a <c>map</c> value.
    /// </summary>
    /// <param name="value">The map when the method returns <see langword="true"/>; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when <see cref="Kind"/> is <see cref="CelValueKind.Map"/>.</returns>
    public bool TryGetMap([NotNullWhen(true)] out CelMap? value)
    {
        value = _kind == CelValueKind.Map ? (CelMap?)_ref : null;
        return value is not null;
    }

    /// <summary>
    /// Reads a type value. Its parameters are erased: the type value of <c>[1]</c> is <c>list</c> without
    /// an element type.
    /// </summary>
    /// <param name="value">The type when the method returns <see langword="true"/>; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when <see cref="Kind"/> is <see cref="CelValueKind.Type"/>.</returns>
    public bool TryGetType([NotNullWhen(true)] out CelType? value)
    {
        value = _kind == CelValueKind.Type ? (CelType?)_ref : null;
        return value is not null;
    }

    /// <summary>
    /// Reads an <c>optional_type(T)</c> value.
    /// </summary>
    /// <param name="value">The optional when the method returns <see langword="true"/>; otherwise an empty optional.</param>
    /// <returns><see langword="true"/> when <see cref="Kind"/> is <see cref="CelValueKind.Optional"/>.</returns>
    public bool TryGetOptional(out CelOptional<CelValue> value)
    {
        value = _kind == CelValueKind.Optional && _ref is OptionalBox box ? new CelOptional<CelValue>(box.Value) : default;
        return _kind == CelValueKind.Optional;
    }

    /// <summary>
    /// Reads an error value. Such values appear inside evaluation, for example when an element of a list
    /// over application data cannot be read. A function implementation that returns an error value it
    /// received makes its call evaluate to that error.
    /// </summary>
    /// <param name="value">The error when the method returns <see langword="true"/>; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when <see cref="Kind"/> is <see cref="CelValueKind.Error"/>.</returns>
    public bool TryGetError([NotNullWhen(true)] out CelError? value)
    {
        value = _kind == CelValueKind.Error ? (CelError?)_ref : null;
        return value is not null;
    }

    /// <summary>
    /// Creates an error value: the value of a part of an expression whose evaluation failed.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <returns>The value of the kind <see cref="CelValueKind.Error"/>.</returns>
    internal static CelValue FromError(CelError error)
    {
        return new CelValue(CelValueKind.Error, error, 0, 0);
    }

    // The value of an abstract type: the object is the whole value, so no payload is stored beside it.
    internal static CelValue FromAbstract(AbstractValue value) => new(CelValueKind.Abstract, value, 0, 0);

    // The object of a value of the kind Abstract; null for any other kind.
    internal bool TryGetAbstract([NotNullWhen(true)] out AbstractValue? value)
    {
        value = _kind == CelValueKind.Abstract ? (AbstractValue?)_ref : null;
        return value is not null;
    }

#if NET11_0_OR_GREATER
    /// <summary>
    /// Reads a <c>decimal</c> value exactly, with its digits and exponent. Only on .NET 11.
    /// </summary>
    /// <param name="value">The value when the method returns <see langword="true"/>; otherwise zero.</param>
    /// <returns><see langword="true"/> when <see cref="Kind"/> is <see cref="CelValueKind.Decimal"/>.</returns>
    public bool TryGetDecimal(out Decimal128 value)
    {
        throw new NotImplementedException();
    }
#endif

    /// <summary>
    /// Compares two values by the equality of the CEL language definition, the <c>==</c> operator of an
    /// expression.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>int</c>, <c>uint</c> and <c>double</c> values compare on one number line; a NaN equals nothing;
    /// values of different non-numeric kinds are not equal, without an error; lists compare element by
    /// element, maps by their keys and values; objects compare by the fields of their type description,
    /// not by reference; a <c>decimal</c> equals only a <c>decimal</c>.
    /// </para>
    /// <para>
    /// The result is a <c>bool</c> value, or an error value when the comparison reads application data
    /// that fails — an element of a list or map over application data that cannot be read, a JSON
    /// object with a repeated property name — or when the values are nested too deeply for the stack
    /// of the thread.
    /// </para>
    /// </remarks>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>A value of the kind <see cref="CelValueKind.Bool"/> or <see cref="CelValueKind.Error"/>.</returns>
    public static CelValue CelEquals(CelValue left, CelValue right) =>
        EqualityRules.ToValue(EqualityRules.AreEqual(left, right, null, out CelValue error), error);

    /// <summary>
    /// Compares two values by .NET equality: the same kind and the same content. Numbers, booleans,
    /// timestamps, durations and decimals compare by value, and a NaN equals itself; strings compare by
    /// ordinal comparison; bytes compare by content; types compare by <see cref="CelType.Equals(CelType)"/>;
    /// optionals compare by their content; lists, maps, objects and errors compare by reference.
    /// </summary>
    /// <remarks>
    /// This equality is reflexive and does not read application data, so values can be keys of .NET
    /// dictionaries. It is not the CEL <c>==</c>; for that, use <see cref="CelEquals"/>.
    /// </remarks>
    /// <param name="other">The value to compare with.</param>
    /// <returns><see langword="true"/> when the values are equal.</returns>
    public bool Equals(CelValue other)
    {
        if (_kind != other._kind)
        {
            return false;
        }

        return _kind switch
        {
            CelValueKind.Null => true,
            CelValueKind.Bool or CelValueKind.Int or CelValueKind.UInt or CelValueKind.Duration => _p0 == other._p0,
            CelValueKind.Timestamp => _p0 == other._p0 && _p1 == other._p1,
            CelValueKind.Double => BitConverter.UInt64BitsToDouble(_p0).Equals(BitConverter.UInt64BitsToDouble(other._p0)),
            CelValueKind.String => string.Equals((string?)_ref, (string?)other._ref, StringComparison.Ordinal),
            CelValueKind.Bytes => BytesOf(this).Span.SequenceEqual(BytesOf(other).Span),
            CelValueKind.Type => ((CelType)_ref!).Equals((CelType?)other._ref),
            CelValueKind.List or CelValueKind.Map or CelValueKind.Error => ReferenceEquals(_ref, other._ref),
            CelValueKind.Object => ObjectEquals(other),
            CelValueKind.Abstract => ((AbstractValue)_ref!).ContentEquals((AbstractValue)other._ref!),
            CelValueKind.Optional => OptionalEquals(other),

            // No factory creates values of the other kinds yet.
            _ => throw new NotImplementedException(),
        };
    }

    /// <summary>
    /// Compares this value with an object, as <see cref="Equals(CelValue)"/> does when the object is a <see cref="CelValue"/>.
    /// </summary>
    /// <param name="obj">The object to compare with.</param>
    /// <returns><see langword="true"/> when <paramref name="obj"/> is an equal value.</returns>
    public override bool Equals(object? obj)
    {
        return obj is CelValue other && Equals(other);
    }

    /// <summary>
    /// Returns a hash code consistent with <see cref="Equals(CelValue)"/>.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        int content = _kind switch
        {
            CelValueKind.Null => 0,
            CelValueKind.Bool or CelValueKind.Int or CelValueKind.UInt or CelValueKind.Duration => _p0.GetHashCode(),
            CelValueKind.Timestamp => HashCode.Combine(_p0, _p1),
            CelValueKind.Double => BitConverter.UInt64BitsToDouble(_p0).GetHashCode(),
            CelValueKind.String => ((string)_ref!).GetHashCode(StringComparison.Ordinal),
            CelValueKind.Bytes => BytesHash(BytesOf(this).Span),
            CelValueKind.Type => ((CelType)_ref!).GetHashCode(),
            CelValueKind.List or CelValueKind.Map or CelValueKind.Error => RuntimeHelpers.GetHashCode(_ref),
            CelValueKind.Object => _ref is ObjectPair pair
                ? HashCode.Combine(RuntimeHelpers.GetHashCode(pair.Instance), RuntimeHelpers.GetHashCode(pair.Type))
                : RuntimeHelpers.GetHashCode(_ref),
            CelValueKind.Abstract => ((AbstractValue)_ref!).ContentHash(),
            CelValueKind.Optional => _ref is OptionalBox box ? box.Value.GetHashCode() : 0,

            // No factory creates values of the other kinds yet.
            _ => throw new NotImplementedException(),
        };
        return HashCode.Combine(_kind, content);
    }

    /// <summary>
    /// Tells whether two values are equal by .NET equality, as <see cref="Equals(CelValue)"/> does; not the CEL <c>==</c>.
    /// </summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/> when the values are equal.</returns>
    public static bool operator ==(CelValue left, CelValue right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Tells whether two values differ by .NET equality, as the negation of <see cref="Equals(CelValue)"/>.
    /// </summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/> when the values differ.</returns>
    public static bool operator !=(CelValue left, CelValue right)
    {
        return !left.Equals(right);
    }

    // Two empty optionals, or two that hold values equal by .NET equality; an empty and a non-empty optional differ.
    private bool OptionalEquals(CelValue other) => _ref is OptionalBox box
        ? other._ref is OptionalBox otherBox && box.Value.Equals(otherBox.Value)
        : other._ref is null;

    // The same instance, and for a pair the same description; a bare instance never equals a pair.
    private bool ObjectEquals(CelValue other)
    {
        if (_ref is ObjectPair pair)
        {
            return other._ref is ObjectPair otherPair
                && ReferenceEquals(pair.Instance, otherPair.Instance)
                && ReferenceEquals(pair.Type, otherPair.Type);
        }

        return other._ref is not ObjectPair && ReferenceEquals(_ref, other._ref);
    }

    private static ReadOnlyMemory<byte> BytesOf(CelValue value)
    {
        _ = value.TryGetBytes(out ReadOnlyMemory<byte> bytes);
        return bytes;
    }

    private static int BytesHash(ReadOnlySpan<byte> bytes)
    {
        var hash = default(HashCode);
        hash.AddBytes(bytes);
        return hash.ToHashCode();
    }
}

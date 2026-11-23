using System;
using System.Numerics;
using Great.Parts.Cel.Diagnostics;

namespace Great.Parts.Cel.Values;

public abstract partial class CelObjectType
{
    /// <summary>
    /// Converts a <see cref="DateOnly"/> member to a <c>date</c> value, exactly.
    /// </summary>
    /// <remarks>
    /// An expression reads such a member only in an environment with the <c>calendar</c> extension; without it,
    /// selecting the field is a type checking error <see cref="CelErrorCode.FieldNotAccessible"/> that names the
    /// extension, and this conversion is not called.
    /// </remarks>
    /// <param name="value">The value of the member.</param>
    /// <returns>A value of the kind <see cref="CelValueKind.Date"/>.</returns>
    protected static CelValue FromDate(DateOnly value)
    {
        return CelValue.FromDate(value);
    }

    /// <summary>
    /// Converts a <see cref="TimeOnly"/> member to a <c>time_of_day</c> value, exactly.
    /// </summary>
    /// <remarks>
    /// An expression reads such a member only in an environment with the <c>calendar</c> extension, as for
    /// <see cref="FromDate(DateOnly)"/>.
    /// </remarks>
    /// <param name="value">The value of the member.</param>
    /// <returns>A value of the kind <see cref="CelValueKind.TimeOfDay"/>.</returns>
    protected static CelValue FromTimeOfDay(TimeOnly value)
    {
        return CelValue.FromTimeOfDay(value);
    }

    /// <summary>
    /// Converts a <see cref="char"/> member to a <c>string</c> value of that one character.
    /// </summary>
    /// <remarks>
    /// A CEL string is a sequence of Unicode code points, and a lone surrogate (U+D800 to U+DFFF) is not one: such a
    /// member gives an error value with the code <see cref="CelErrorCode.InvalidArgument"/> that names the type and the
    /// field.
    /// </remarks>
    /// <param name="number">The number of the field, for the error.</param>
    /// <param name="value">The value of the member.</param>
    /// <returns>A value of the kind <see cref="CelValueKind.String"/> or <see cref="CelValueKind.Error"/>.</returns>
    protected CelValue FromChar(int number, char value)
    {
        return Named(number, WideRules.FromChar(value));
    }

    /// <summary>
    /// Converts an <see cref="Int128"/> member to an <c>int</c> value.
    /// </summary>
    /// <remarks>
    /// CEL has no integers wider than 64 bits: a value outside the range of <see cref="long"/> gives an error value with
    /// the code <see cref="CelErrorCode.ConversionRange"/> that names the type and the field, never a truncated number.
    /// </remarks>
    /// <param name="number">The number of the field, for the error.</param>
    /// <param name="value">The value of the member.</param>
    /// <returns>A value of the kind <see cref="CelValueKind.Int"/> or <see cref="CelValueKind.Error"/>.</returns>
    protected CelValue FromInt128(int number, Int128 value)
    {
        return WideRules.TryFromInt128(value, out CelValue result) ? result : Named(number, WideRules.OutOfRange(value, Fields[number].Name));
    }

    /// <summary>
    /// Converts a <see cref="UInt128"/> member to a <c>uint</c> value.
    /// </summary>
    /// <remarks>
    /// A value above <see cref="ulong.MaxValue"/> gives an error value with the code
    /// <see cref="CelErrorCode.ConversionRange"/> that names the type and the field.
    /// </remarks>
    /// <param name="number">The number of the field, for the error.</param>
    /// <param name="value">The value of the member.</param>
    /// <returns>A value of the kind <see cref="CelValueKind.UInt"/> or <see cref="CelValueKind.Error"/>.</returns>
    protected CelValue FromUInt128(int number, UInt128 value)
    {
        return WideRules.TryFromUInt128(value, out CelValue result) ? result : Named(number, WideRules.OutOfRange(value, Fields[number].Name));
    }

    /// <summary>
    /// Converts a <see cref="BigInteger"/> member to an <c>int</c> value.
    /// </summary>
    /// <remarks>
    /// The CEL type of the member is <c>int</c> whatever its value, so a value outside the range of <see cref="long"/>,
    /// including one that would fit an <c>uint</c>, gives an error value with the code
    /// <see cref="CelErrorCode.ConversionRange"/> that names the type and the field. No memory is allocated.
    /// </remarks>
    /// <param name="number">The number of the field, for the error.</param>
    /// <param name="value">The value of the member.</param>
    /// <returns>A value of the kind <see cref="CelValueKind.Int"/> or <see cref="CelValueKind.Error"/>.</returns>
    protected CelValue FromBigInteger(int number, BigInteger value)
    {
        return WideRules.TryFromBigInteger(value, out CelValue result) ? result : Named(number, WideRules.OutOfRange(value, Fields[number].Name));
    }

    /// <summary>
    /// Tells whether an object literal can set a field: type checking calls it for every field a literal sets when
    /// <see cref="IsConstructible"/> is <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// The default is <see langword="true"/>: every field can be set, as for protobuf messages. Generated code returns
    /// <see langword="false"/> for a member without a public <see langword="set"/> or <see langword="init"/> accessor
    /// and not a parameter of the constructor it calls, and for a member whose .NET type it cannot create from a CEL
    /// value. A literal that sets such a field is a type checking error <see cref="CelErrorCode.FieldNotAccessible"/>
    /// that gives the reason.
    /// </remarks>
    /// <param name="number">The number of one of <see cref="Fields"/>.</param>
    /// <returns><see langword="true"/> when an object literal can set the field.</returns>
    protected internal virtual bool CanSetField(int number) => true;

    /// <summary>
    /// Tells whether an object literal must set a field: type checking calls it for every field of the type when
    /// <see cref="IsConstructible"/> is <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// The default is <see langword="false"/>. Generated code returns <see langword="true"/> for a
    /// <see langword="required"/> member and for a parameter of the constructor it calls that has no default value. A
    /// literal that does not set such a field is a type checking error
    /// <see cref="CelErrorCode.RequiredFieldNotSet"/> that names the type and the field.
    /// </remarks>
    /// <param name="number">The number of one of <see cref="Fields"/>.</param>
    /// <returns><see langword="true"/> when every object literal of the type must set the field.</returns>
    protected internal virtual bool IsRequiredField(int number) => false;
}

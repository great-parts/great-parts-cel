using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Values;

/// <summary>
/// CEL values to the requested .NET type, by the result rows of
/// the <see href="https://github.com/great-parts/great-parts-cel/blob/main/docs/type-mapping.md">type mapping</see>, and
/// the expected CEL type of a .NET type by its expected-type rows. There is no conversion between numeric types: a
/// <c>uint</c> is not a <c>long</c>.
/// </summary>
internal static partial class ConversionRules
{
    /// <summary>
    /// The expected CEL type of the result of <c>Compile&lt;T&gt;</c>: from <typeparamref name="T"/> by the expected-type
    /// table, or the explicit type of the compile options when <typeparamref name="T"/> holds its values.
    /// </summary>
    /// <typeparam name="T">The requested .NET type.</typeparam>
    /// <param name="explicitType">The explicit expected type, or <see langword="null"/> for none.</param>
    /// <param name="parameterName">The name of the parameter that holds the explicit type, for the exception.</param>
    /// <param name="types">
    /// The type index of the environment: a class, struct or interface outside <c>System</c> whose exact .NET type it
    /// describes expects its object type; another such type expects <c>dyn</c>, and its instance is tested at evaluation.
    /// </param>
    /// <returns>The expected type.</returns>
    /// <exception cref="ArgumentException">
    /// <typeparamref name="T"/> is outside the tables, or it does not hold the values of the explicit type.
    /// </exception>
    /// <exception cref="NotImplementedException">
    /// <typeparamref name="T"/> or the explicit type is not supported yet: <c>decimal</c>.
    /// </exception>
    internal static CelType ExpectedType<T>(CelType? explicitType, string parameterName, TypeIndex? types = null)
    {
        if (IsApplicationType<T>())
        {
            return ApplicationExpectedType<T>(explicitType, parameterName, types);
        }

        ClrRow<T> row = ResultRow<T>();
        if (explicitType is null)
        {
            return row.ExpectedType;
        }

        if (MentionsLaterPart(explicitType))
        {
            throw new NotImplementedException();
        }

        if (!row.Holds(explicitType))
        {
            throw new ArgumentException(
                "The expected type '" + explicitType + "' cannot be converted to the result type '" + typeof(T) + "'.",
                parameterName);
        }

        return explicitType;
    }

    /// <summary>
    /// The CEL type of a parameter of a typed delegate: the expected type of its .NET type, as for <c>Compile&lt;T&gt;</c>.
    /// </summary>
    /// <typeparam name="T">The .NET type of the parameter.</typeparam>
    /// <returns>The CEL type; <see langword="null"/> when <typeparamref name="T"/> is outside the expected-type table.</returns>
    internal static CelType? ParameterType<T>() => RowOf<T>.Row?.ExpectedType;

    /// <summary>
    /// Whether <typeparamref name="T"/> is a reference type with a wrapper, <c>string</c> or <c>byte[]</c>: as a parameter
    /// or the result of a typed delegate it fits a declared <c>wrapper(P)</c> of its type <c>P</c> too, since it takes
    /// <see langword="null"/>.
    /// </summary>
    /// <typeparam name="T">The .NET type of the parameter or the result.</typeparam>
    /// <returns><see langword="true"/> for <c>string</c> and <c>byte[]</c>.</returns>
    internal static bool TakesNullReference<T>() => typeof(T) == typeof(string) || typeof(T) == typeof(byte[]);

    /// <summary>
    /// Converts the value of the root of an evaluation to <typeparamref name="T"/> by the row of its CEL type. A list or
    /// a map whose elements cannot fail their conversion by the static type of the root is a lazy view, and before it the
    /// value is checked at any depth, without a meter, against the expected type of <typeparamref name="T"/> — what the
    /// view reads by a .NET type — with <c>null</c> fitting an element or a value that the view reads as a null reference;
    /// otherwise it is an immutable copy. So the choice between a view and a copy does not change the outcome.
    /// </summary>
    /// <typeparam name="T">The requested .NET type, which <see cref="ExpectedType{T}"/> accepted.</typeparam>
    /// <param name="value">The value, not an error value.</param>
    /// <param name="resultType">The static type of the root.</param>
    /// <param name="result">The converted value when the method returns <see langword="true"/>.</param>
    /// <param name="actualType">
    /// When the method returns <see langword="false"/>, the CEL type that the error names: the type that the check
    /// before a lazy view found, or the name of the type of the value.
    /// </param>
    /// <param name="types">The type index of the environment, which names the description of an object value.</param>
    /// <param name="elementError">
    /// When the method returns <see langword="false"/> because the check met an error value as an element, a key or a value
    /// of a collection of the application, that error, the first in the order of the check — a list by index, a map in the
    /// order of iteration; it is no mismatch, and the caller returns it as the value read; otherwise <see langword="null"/>.
    /// </param>
    /// <returns><see langword="false"/> when the value does not convert.</returns>
    internal static bool TryToResult<T>(
        CelValue value,
        CelType resultType,
        [MaybeNull] out T result,
        [NotNullWhen(false)] out string? actualType,
        out CelError? elementError,
        TypeIndex? types = null)
    {
        elementError = null;
        if (typeof(T) == typeof(CelValue))
        {
            result = Unsafe.As<CelValue, T>(ref value);
            actualType = null;
            return true;
        }

        if (IsApplicationType<T>())
        {
            return TryToApplicationType(value, out result, out actualType, types);
        }

        ClrRow<T> row = RowOf<T>.Row!;
        if (row.IsCheckedView(resultType))
        {
            TypeFit fit = ValueTypeRules.Fits(value, row.ExpectedType, null, row.NullElements);
            if (!fit.IsFit)
            {
                result = default;
                elementError = fit.ElementError;
                actualType = ValueTypeRules.MismatchType(value, row.ExpectedType, row.NullElements, types) ?? TypeName(value, types);
                return false;
            }
        }

        if (row.TryConvert(value, resultType, null, out result))
        {
            actualType = null;
            return true;
        }

        // A copy fails at an element that does not convert; an error value among the elements is the outcome when the
        // check of the expected type reaches it first. The walk runs only on this path, so a result that converts pays nothing.
        elementError = value.Kind is CelValueKind.List or CelValueKind.Map
            ? ValueTypeRules.Fits(value, row.ExpectedType, null, row.NullElements, types).ElementError
            : null;
        actualType = TypeName(value, types);
        return false;
    }

    // Whether T is a class, record, struct or interface outside System and outside the library: a result type of the
    // row of object values.
    private static bool IsApplicationType<T>() => RowOf<T>.Row is null && IsApplicationObject(typeof(T));

    // The expected type of an application type: its object type when a source describes exactly this .NET type, else
    // dyn. An explicit type must be dyn, a type parameter or an object type.
    private static CelType ApplicationExpectedType<T>(CelType? explicitType, string parameterName, TypeIndex? types)
    {
        if (explicitType is null)
        {
            return types is not null && types.TryGetByClrType(typeof(T), out CelObjectType? described) ? described.Type : CelType.Dyn;
        }

        if (MentionsLaterPart(explicitType))
        {
            throw new NotImplementedException();
        }

        if (explicitType.Kind is not (CelTypeKind.Dyn or CelTypeKind.TypeParameter or CelTypeKind.Object))
        {
            throw new ArgumentException(
                "The expected type '" + explicitType + "' cannot be converted to the result type '" + typeof(T) + "'.",
                parameterName);
        }

        return explicitType;
    }

    // The row of object values, a type described at run time included: the instance itself when it is a T, by an
    // instance test, without reading the description; null for a T that takes null. A refusal names the description of
    // an object value, or the .NET type of an instance without one, never TypeNotAccessible.
    private static bool TryToApplicationType<T>(CelValue value, [MaybeNull] out T result, [NotNullWhen(false)] out string? actualType, TypeIndex? types)
    {
        actualType = null;
        result = default;
        if (value.TryGetObject(out object? instance) && instance is T converted)
        {
            result = converted;
            return true;
        }

        if (value.Kind == CelValueKind.Null && !typeof(T).IsValueType)
        {
            return true;
        }

        actualType = TypeName(value, types);
        return false;
    }

    // The name of the type of a value in the message of ResultConversion: of an object value its description, or the
    // .NET type of an instance without one; of an optional that holds a value, optional_type with the name of the type of the held
    // value, such as optional_type(string).
    private static string TypeName(CelValue value, TypeIndex? types) =>
        value.TryGetOptional(out CelOptional<CelValue> optional) && optional.HasValue
            ? "optional_type(" + TypeName(optional.Value, types) + ")"
            : ValueTypeRules.NameOf(value, types);

    /// <summary>
    /// Converts an argument of a typed delegate to its parameter type <typeparamref name="T"/>. The argument fits the
    /// declared parameter type, which is the expected type of <typeparamref name="T"/>: a collection whose elements
    /// cannot fail is a lazy view and costs nothing, any other is a copy charged first, a list of n elements 1 + n and a
    /// map of n pairs 1 + 2n and ⌈L/10⌉ for each string key of length L; a <c>byte[]</c> of L bytes is a copy charged
    /// 1 + ⌈L/10⌉.
    /// </summary>
    /// <typeparam name="T">The .NET type of the parameter.</typeparam>
    /// <param name="value">The argument, not an error value.</param>
    /// <param name="meter">The meter of an evaluation with a cost limit; <see langword="null"/> charges nothing.</param>
    /// <param name="result">The converted value when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="false"/> when a row refuses the value, such as an <c>int</c> outside <c>Int32</c>.</returns>
    internal static bool TryToParameter<T>(CelValue value, IEvaluationMeter? meter, [MaybeNull] out T result)
    {
        if (typeof(T) == typeof(CelValue))
        {
            result = Unsafe.As<CelValue, T>(ref value);
            return true;
        }

        if (IsPrimitive<T>())
        {
            return TryToPrimitive(value, meter, out result);
        }

        ClrRow<T>? row = RowOf<T>.Row;
        if (row is null)
        {
            // The build accepts only parameter types of the table and the types of the application that a type source
            // describes: the instance itself, by an instance test.
            return TryToApplicationType(value, out result, out _, null);
        }

        return row.TryConvert(value, row.ExpectedType, meter, out result);
    }

    /// <summary>
    /// Converts a value that is not an error value to <typeparamref name="T"/> by the row of its CEL type, as a value of
    /// type <c>dyn</c>: a list or a map of a typed collection is a copy.
    /// </summary>
    /// <typeparam name="T">The requested .NET type.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="result">
    /// The converted value when the method returns <see langword="true"/>, <see langword="null"/> for a CEL
    /// <c>null</c> as a reference type or a <c>Nullable&lt;X&gt;</c>; otherwise the default value.
    /// </param>
    /// <returns><see langword="false"/> when no row converts the value to <typeparamref name="T"/>.</returns>
    internal static bool TryToClr<T>(CelValue value, [MaybeNull] out T result)
    {
        if (typeof(T) == typeof(CelValue))
        {
            result = Unsafe.As<CelValue, T>(ref value);
            return true;
        }

        if (IsPrimitive<T>())
        {
            return TryToPrimitive(value, null, out result);
        }

        if (RowOf<T>.Row is ClrRow<T> row)
        {
            return row.TryConvert(value, CelType.Dyn, null, out result);
        }

        result = default;
        return false;
    }

    /// <summary>
    /// Creates the error <see cref="CelErrorCode.ResultConversion"/> for a value that does not convert, naming the type of
    /// the value.
    /// </summary>
    /// <typeparam name="T">The requested .NET type.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="location">The position of the root of the expression.</param>
    /// <returns>The error of the stage <see cref="CelErrorStage.Evaluate"/>.</returns>
    internal static CelError ResultConversionError<T>(CelValue value, CelSourceLocation location) =>
        ResultConversionError<T>(ValueTypeRules.NameOf(value, null), location);

    /// <summary>
    /// Creates the error <see cref="CelErrorCode.ResultConversion"/> naming the given CEL type.
    /// </summary>
    /// <typeparam name="T">The requested .NET type.</typeparam>
    /// <param name="celType">The CEL type that the message names.</param>
    /// <param name="location">The position of the root of the expression.</param>
    /// <returns>The error of the stage <see cref="CelErrorStage.Evaluate"/>.</returns>
    internal static CelError ResultConversionError<T>(string celType, CelSourceLocation location)
    {
        string clrType = typeof(T) == typeof(ReadOnlyMemory<byte>) ? "System.ReadOnlyMemory<System.Byte>" : typeof(T).ToString();
        string message = string.Create(
            CultureInfo.InvariantCulture,
            $"result of type '{celType}' cannot be converted to .NET type '{clrType}'");
        return new CelError(CelErrorCode.ResultConversion, CelErrorStage.Evaluate, message, location, null, null);
    }

    // The row of a result type, or the exception of the derive step.
    private static ClrRow<T> ResultRow<T>()
    {
        if (RowOf<T>.Row is ClrRow<T> row)
        {
            return row;
        }

        if (MentionsLaterPart(typeof(T)))
        {
            // The result types of decimal come with their part; they are not ignored.
            throw new NotImplementedException();
        }

        throw new ArgumentException("The type '" + typeof(T) + "' is not a result type of the type mapping.", nameof(T));
    }

    // The primitive rows, which convert without a row object: bool, the integers, double, string and bytes.
    private static bool IsPrimitive<T>() =>
        typeof(T) == typeof(bool) || typeof(T) == typeof(long) || typeof(T) == typeof(int) || typeof(T) == typeof(ulong)
        || typeof(T) == typeof(uint) || typeof(T) == typeof(double) || typeof(T) == typeof(string)
        || typeof(T) == typeof(ReadOnlyMemory<byte>) || typeof(T) == typeof(byte[]);

    // A primitive row; a CEL null as a string or a byte array is null. A byte array is a copy, charged to the meter of
    // an argument first.
    private static bool TryToPrimitive<T>(CelValue value, IEvaluationMeter? meter, [MaybeNull] out T result)
    {
        if (value.Kind == CelValueKind.Null && (typeof(T) == typeof(string) || typeof(T) == typeof(byte[])))
        {
            result = default;
            return true;
        }

        return TryToInteger(value, out result) || TryToOther(value, meter, out result);
    }

    // long and int from an int, ulong and uint from a uint, each with the range check of the narrower type.
    private static bool TryToInteger<T>(CelValue value, [MaybeNull] out T result)
    {
        result = default;
        if (value.TryGetInt64(out long intValue))
        {
            if (typeof(T) == typeof(long))
            {
                result = Unsafe.As<long, T>(ref intValue);
                return true;
            }

            if (typeof(T) == typeof(int) && intValue is >= int.MinValue and <= int.MaxValue)
            {
                int narrow = (int)intValue;
                result = Unsafe.As<int, T>(ref narrow);
                return true;
            }
        }
        else if (value.TryGetUInt64(out ulong uintValue))
        {
            if (typeof(T) == typeof(ulong))
            {
                result = Unsafe.As<ulong, T>(ref uintValue);
                return true;
            }

            if (typeof(T) == typeof(uint) && uintValue <= uint.MaxValue)
            {
                uint narrow = (uint)uintValue;
                result = Unsafe.As<uint, T>(ref narrow);
                return true;
            }
        }

        return false;
    }

    // bool, double, string and bytes, each only from its own CEL type.
    private static bool TryToOther<T>(CelValue value, IEvaluationMeter? meter, [MaybeNull] out T result)
    {
        result = default;
        if (typeof(T) == typeof(bool) && value.TryGetBoolean(out bool boolValue))
        {
            result = Unsafe.As<bool, T>(ref boolValue);
        }
        else if (typeof(T) == typeof(double) && value.TryGetDouble(out double doubleValue))
        {
            result = Unsafe.As<double, T>(ref doubleValue);
        }
        else if (typeof(T) == typeof(string) && value.TryGetString(out string? text))
        {
            result = (T)(object)text;
        }
        else if (typeof(T) == typeof(ReadOnlyMemory<byte>) && value.TryGetBytes(out ReadOnlyMemory<byte> memory))
        {
            result = Unsafe.As<ReadOnlyMemory<byte>, T>(ref memory);
        }
        else if (typeof(T) == typeof(byte[]) && value.TryGetBytes(out ReadOnlyMemory<byte> copied))
        {
            result = (T)(object)CopyBytes(copied, meter);
        }
        else
        {
            return false;
        }

        return true;
    }
}

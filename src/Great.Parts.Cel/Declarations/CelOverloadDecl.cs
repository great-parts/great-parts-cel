using System;
using System.Collections.Immutable;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Types;

namespace Great.Parts.Cel.Declarations;

/// <summary>
/// The declaration of one overload of a function: its identifier, call style, parameter types, result
/// type and, optionally, the cost it adds to an evaluation.
/// </summary>
/// <remarks>
/// <para>
/// An overload is called in one of two styles, as in the CEL language definition: global, such as
/// <c>isInn(order.inn)</c>, or on a receiver, such as <c>order.inn.isInn()</c>. Parameter types can contain
/// type parameters, created with <see cref="CelType.TypeParameter"/>, which make the overload generic:
/// type checking binds them anew at each call, for example <c>(list(A)) -&gt; A</c>. Type checking selects
/// among the overloads of a function those whose parameter types fit the arguments, and evaluation calls
/// only an implementation of one of the selected overloads.
/// </para>
/// <para>
/// The overload identifier binds the declaration to its implementation and names the overload in
/// errors. It is unique in the whole environment — across declarations of the application, the
/// environment description read from JSON, extension sets and the selected version of the standard
/// library: one identifier for two different overloads is a build error
/// <see cref="CelErrorCode.DuplicateOverloadId"/>, while the same overload declared twice is one
/// declaration. Two overloads of a function whose parameter types are the same or overlap after type
/// parameters are erased are a build error <see cref="CelErrorCode.OverlappingOverloads"/>, or
/// <see cref="CelErrorCode.StandardLibraryConflict"/> when one of them belongs to the standard library.
/// An overload declared without an implementation in the environment is a build error
/// <see cref="CelErrorCode.MissingImplementation"/>.
/// </para>
/// <para>The declaration is immutable and can be shared between threads and environments.</para>
/// </remarks>
public sealed class CelOverloadDecl
{
    // The declarations of the standard library tables, which are valid by construction, and the checked
    // arguments of the factories.
    internal CelOverloadDecl(
        string overloadId,
        bool isReceiverStyle,
        ImmutableArray<CelType> parameterTypes,
        CelType resultType,
        CelCostFunction? cost = null)
    {
        OverloadId = overloadId;
        IsReceiverStyle = isReceiverStyle;
        ParameterTypes = parameterTypes;
        ResultType = resultType;
        Cost = cost;
    }

    /// <summary>
    /// The overload identifier, such as <c>isInn_string</c>.
    /// </summary>
    public string OverloadId { get; }

    /// <summary>
    /// Whether the overload is called on a receiver, as <c>x.f(…)</c>, rather than globally, as
    /// <c>f(…)</c>. For a receiver overload the first element of <see cref="ParameterTypes"/> is the type of
    /// the receiver.
    /// </summary>
    public bool IsReceiverStyle { get; }

    /// <summary>
    /// The types of the parameters in call order; for a receiver overload, the receiver comes first.
    /// The implementation receives its arguments in the same order.
    /// </summary>
    public ImmutableArray<CelType> ParameterTypes { get; }

    /// <summary>
    /// The type of the result.
    /// </summary>
    public CelType ResultType { get; }

    /// <summary>
    /// The function that tells the cost of a call of this overload, or <see langword="null"/> when a call
    /// costs only the fixed unit of an application function call.
    /// </summary>
    /// <remarks>
    /// A call of an application function costs 1 unit plus the value of this function, which is computed
    /// from the arguments before the implementation is called; see <see cref="CelCostFunction"/>.
    /// </remarks>
    public CelCostFunction? Cost { get; }

    /// <summary>
    /// Declares an overload called globally, as <c>f(a, b)</c>.
    /// </summary>
    /// <param name="overloadId">
    /// The overload identifier, unique in the environment, such as <c>isInn_string</c>. An implementation
    /// registered under this identifier implements the overload.
    /// </param>
    /// <param name="parameterTypes">The types of the parameters in call order; empty for a function without arguments.</param>
    /// <param name="resultType">The type of the result.</param>
    /// <returns>The declaration.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="overloadId"/>, <paramref name="resultType"/> or an element of <paramref name="parameterTypes"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="overloadId"/> is empty.</exception>
    public static CelOverloadDecl Global(string overloadId, ReadOnlySpan<CelType> parameterTypes, CelType resultType)
    {
        ArgumentException.ThrowIfNullOrEmpty(overloadId);
        ArgumentNullException.ThrowIfNull(resultType);
        return new CelOverloadDecl(overloadId, false, Parameters(null, parameterTypes), resultType);
    }

    /// <summary>
    /// Declares an overload called on a receiver, as <c>x.f(a, b)</c>.
    /// </summary>
    /// <param name="overloadId">
    /// The overload identifier, unique in the environment, such as <c>string_isInn</c>. An implementation
    /// registered under this identifier implements the overload.
    /// </param>
    /// <param name="receiverType">The type of the receiver, the value before the dot.</param>
    /// <param name="parameterTypes">The types of the other parameters in call order; empty for a call without arguments.</param>
    /// <param name="resultType">The type of the result.</param>
    /// <returns>The declaration; its <see cref="ParameterTypes"/> start with <paramref name="receiverType"/>.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="overloadId"/>, <paramref name="receiverType"/>, <paramref name="resultType"/> or an element of
    /// <paramref name="parameterTypes"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="overloadId"/> is empty.</exception>
    public static CelOverloadDecl Receiver(string overloadId, CelType receiverType, ReadOnlySpan<CelType> parameterTypes, CelType resultType)
    {
        ArgumentException.ThrowIfNullOrEmpty(overloadId);
        ArgumentNullException.ThrowIfNull(receiverType);
        ArgumentNullException.ThrowIfNull(resultType);
        return new CelOverloadDecl(overloadId, true, Parameters(receiverType, parameterTypes), resultType);
    }

    /// <summary>
    /// Returns a declaration that differs from this one only by the function that tells the cost of a call.
    /// This declaration does not change.
    /// </summary>
    /// <param name="cost">The function that computes the cost of a call from its arguments.</param>
    /// <returns>The new declaration.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="cost"/> is <see langword="null"/>.</exception>
    public CelOverloadDecl WithCost(CelCostFunction cost)
    {
        ArgumentNullException.ThrowIfNull(cost);
        return new CelOverloadDecl(OverloadId, IsReceiverStyle, ParameterTypes, ResultType, cost);
    }

    // Whether two declarations are the same overload: one identifier, call style, parameter types, result
    // type and cost function — the same instance, or both none. The same overload declared twice is one.
    internal bool SameAs(CelOverloadDecl other)
    {
        if (!string.Equals(OverloadId, other.OverloadId, StringComparison.Ordinal)
            || IsReceiverStyle != other.IsReceiverStyle
            || ParameterTypes.Length != other.ParameterTypes.Length
            || !ResultType.Equals(other.ResultType)
            || !ReferenceEquals(Cost, other.Cost))
        {
            return false;
        }

        for (int i = 0; i < ParameterTypes.Length; i++)
        {
            if (!ParameterTypes[i].Equals(other.ParameterTypes[i]))
            {
                return false;
            }
        }

        return true;
    }

    // The parameter types, the receiver first when there is one; a null element throws.
    private static ImmutableArray<CelType> Parameters(CelType? receiverType, ReadOnlySpan<CelType> parameterTypes)
    {
        ImmutableArray<CelType>.Builder parameters = ImmutableArray.CreateBuilder<CelType>(parameterTypes.Length + (receiverType is null ? 0 : 1));
        if (receiverType is not null)
        {
            parameters.Add(receiverType);
        }

        foreach (CelType parameterType in parameterTypes)
        {
            ArgumentNullException.ThrowIfNull(parameterType, nameof(parameterTypes));
            parameters.Add(parameterType);
        }

        return parameters.MoveToImmutable();
    }
}

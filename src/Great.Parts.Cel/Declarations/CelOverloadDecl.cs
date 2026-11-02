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
    // The declarations of the standard library tables, which are valid by construction.
    internal CelOverloadDecl(string overloadId, bool isReceiverStyle, ImmutableArray<CelType> parameterTypes, CelType resultType)
    {
        OverloadId = overloadId;
        IsReceiverStyle = isReceiverStyle;
        ParameterTypes = parameterTypes;
        ResultType = resultType;
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
    public CelCostFunction? Cost => throw new NotImplementedException();

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
        throw new NotImplementedException();
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
        throw new NotImplementedException();
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
        throw new NotImplementedException();
    }
}

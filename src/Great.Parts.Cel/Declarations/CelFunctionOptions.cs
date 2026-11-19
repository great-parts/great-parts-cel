namespace Great.Parts.Cel.Declarations;

/// <summary>
/// The options of an overload that the delegate forms <c>AddFunction</c> and <c>AddMethod</c> of
/// <see cref="CelEnvironmentBuilder"/> declare: its cost, its overload identifier and its description.
/// </summary>
/// <remarks>
/// <para>
/// A property left <see langword="null"/> keeps what the delegate form derives: a call costs 1 unit plus the units of the
/// arguments copied for the delegate, the identifier is derived from the function name and the CEL types of the parameters,
/// and the overload has no description.
/// </para>
/// <para>
/// <c>new CelFunctionOptions { OverloadId = "isInn_string", Description = "Tells whether a string is a valid INN." }</c>
/// gives the overload a stable identifier, by which an environment description in JSON can refer to it, and a description
/// that the environment shows in <see cref="CelEnvironment.Functions"/>. The options are read when the builder method is
/// called; changing them later does not change the builder. The type has no other state and can be shared between threads
/// once initialized.
/// </para>
/// </remarks>
public sealed class CelFunctionOptions
{
    /// <summary>
    /// The function that tells the cost of a call from its arguments, in cost units, or <see langword="null"/> for the cost
    /// that the delegate form derives. A call then costs 1 unit plus the value of the function, as for
    /// <see cref="CelOverloadDecl.WithCost(CelCostFunction)"/>.
    /// </summary>
    public CelCostFunction? Cost { get; init; }

    /// <summary>
    /// The overload identifier, unique in the environment, such as <c>isInn_string</c>, or <see langword="null"/> for the
    /// derived one. An explicit identifier stays the same when the .NET signature of the delegate changes, so references from
    /// an environment description in JSON keep working. An empty identifier is an invalid argument of the builder method; an
    /// identifier that another overload already has is the build error
    /// <see cref="Diagnostics.CelErrorCode.DuplicateOverloadId"/>.
    /// </summary>
    public string? OverloadId { get; init; }

    /// <summary>
    /// A description of the overload in English for people — what it computes and from what — or <see langword="null"/> for
    /// none. It becomes <see cref="CelOverloadDecl.Description"/> and is shown by <see cref="CelEnvironment.Functions"/>;
    /// evaluation does not read it.
    /// </summary>
    public string? Description { get; init; }
}

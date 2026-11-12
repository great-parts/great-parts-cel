using System;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Great.Parts.Cel.Diagnostics;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Protobuf;

/// <summary>
/// The description of a protobuf message: an object type whose fields are read through the field accessors of the
/// generated class, by the number of the field in this description, never by name.
/// </summary>
internal sealed class MessageType : CelObjectType
{
    private FieldPlan[] _plans = [];
    private ProviderTypes? _registry;

    internal MessageType(MessageDescriptor descriptor, System.Type clrType, CelObjectField[] fields, bool convertsToValue)
        : base(descriptor.FullName, clrType, fields)
    {
        Descriptor = descriptor;
        ConvertsToValue = convertsToValue;
    }

    /// <summary>
    /// The descriptor of the message.
    /// </summary>
    internal MessageDescriptor Descriptor { get; }

    /// <summary>
    /// <see langword="true"/> for the description of a known type that CEL converts to a value of its own type: its literal is not
    /// a message but that value.
    /// </summary>
    internal bool ConvertsToValue { get; }

    /// <summary>
    /// The plans of the fields, at the numbers of the fields.
    /// </summary>
    internal FieldPlan[] Plans => _plans;

    /// <summary>
    /// What the descriptions of the provider share; set before the description is shared.
    /// </summary>
    internal ProviderTypes Registry => _registry!;

    /// <summary>
    /// Gives the description the plans of its fields; called once, before the description is shared.
    /// </summary>
    /// <param name="plans">One plan per field, at the number of the field.</param>
    internal void Initialize(FieldPlan[] plans)
    {
        _plans = plans;
    }

    /// <summary>
    /// Gives the description the registry of its provider and links the plans to the descriptions of the messages of their values;
    /// called once for every description, after all of them exist and before any is shared.
    /// </summary>
    /// <param name="registry">The registry.</param>
    internal void Attach(ProviderTypes registry)
    {
        _registry = registry;
        foreach (FieldPlan plan in _plans)
        {
            plan.Link(registry);
        }
    }

    /// <summary>
    /// Creates the object value of a message instance, without a description of its own.
    /// </summary>
    /// <param name="instance">The message.</param>
    /// <returns>The object value.</returns>
    internal static CelValue Bare(object instance) => FromInstance(instance);

    /// <summary>
    /// Creates an error value for a field of this description.
    /// </summary>
    /// <param name="number">The number of the field.</param>
    /// <param name="code">The evaluation code.</param>
    /// <param name="detail">What the template of the code needs.</param>
    /// <returns>The error value.</returns>
    internal CelValue Error(int number, CelErrorCode code, string? detail) => FieldError(number, code, detail);

    /// <inheritdoc/>
    protected override CelValue GetField(object instance, int number) => _plans[number].Read((IMessage)instance);

    /// <inheritdoc/>
    protected override bool HasField(object instance, int number) => _plans[number].Has((IMessage)instance);

    /// <inheritdoc/>
    protected override bool IsConstructible => true;

    /// <inheritdoc/>
    protected override CelValue CreateInstance(ReadOnlySpan<int> fieldNumbers, ReadOnlySpan<CelValue> fieldValues) =>
        LiteralWriter.Create(this, fieldNumbers, fieldValues);

    /// <inheritdoc/>
    protected override long FieldReadCost(object instance, int number) => _plans[number].ReadCost((IMessage)instance);

    /// <inheritdoc/>
    protected override long? EqualityCost(object left, object right) => MessageEquality.Cost((IMessage)left, (IMessage)right);

    /// <inheritdoc/>
    protected override CelValue CelEquals(object left, object right) =>
        CelValue.FromBoolean(MessageEquality.Equal(this, (IMessage)left, (IMessage)right));
}

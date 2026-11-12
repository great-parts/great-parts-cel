using System;
using Google.Protobuf;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Protobuf;

/// <summary>
/// Creates the message of an object literal: the empty message, then each field in the order of the literal, converted by the rows
/// of the literal table. The first error ends the creation and is the value.
/// </summary>
internal static class LiteralWriter
{
    /// <summary>
    /// Creates the message, or for the literal of a known type the value that the message converts to.
    /// </summary>
    /// <param name="type">The description of the message.</param>
    /// <param name="fieldNumbers">The distinct numbers of the fields the literal sets, in the order of the literal.</param>
    /// <param name="fieldValues">The values of those fields.</param>
    /// <returns>The message as a bare object, the value of a known type, or an error value.</returns>
    internal static CelValue Create(MessageType type, ReadOnlySpan<int> fieldNumbers, ReadOnlySpan<CelValue> fieldValues)
    {
        IMessage message = type.Descriptor.Parser.ParseFrom(ReadOnlySpan<byte>.Empty);
        FieldPlan[] plans = type.Plans;
        for (int index = 0; index < fieldNumbers.Length; index++)
        {
            if (!plans[fieldNumbers[index]].TryWrite(message, fieldValues[index], out CelValue error))
            {
                return error;
            }
        }

        return type.ConvertsToValue ? KnownValues.FromMessage(message, type, 0) : MessageType.Bare(message);
    }
}

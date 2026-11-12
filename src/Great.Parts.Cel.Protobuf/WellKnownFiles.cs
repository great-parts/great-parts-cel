using Google.Protobuf.Reflection;
using Google.Protobuf.WellKnownTypes;

namespace Great.Parts.Cel.Protobuf;

/// <summary>
/// The .NET type that stands for the description of a well-known message that CEL converts to a value of its own
/// type. No instance of it exists: the description is found by name and never from a value.
/// </summary>
/// <typeparam name="TMessage">The class of the message the description stands for.</typeparam>
internal static class WellKnownMarker<TMessage>
{
}

/// <summary>
/// The files of <c>google/protobuf</c> that every provider describes, and the messages in them.
/// </summary>
internal static class WellKnownFiles
{
    /// <summary>
    /// The seven files of the well-known types that a provider describes whether the application added them or not.
    /// </summary>
    internal static readonly FileDescriptor[] Files =
    [
        AnyReflection.Descriptor,
        DurationReflection.Descriptor,
        EmptyReflection.Descriptor,
        FieldMaskReflection.Descriptor,
        StructReflection.Descriptor,
        TimestampReflection.Descriptor,
        WrappersReflection.Descriptor,
    ];

    /// <summary>
    /// The messages that CEL converts to a value of its own type, each with its marker type. <c>Timestamp</c> and
    /// <c>Duration</c> are not among them: their names are type identifiers of the standard library.
    /// </summary>
    internal static readonly (MessageDescriptor Descriptor, System.Type Marker)[] Converted =
    [
        (Any.Descriptor, typeof(WellKnownMarker<Any>)),
        (Value.Descriptor, typeof(WellKnownMarker<Value>)),
        (Struct.Descriptor, typeof(WellKnownMarker<Struct>)),
        (ListValue.Descriptor, typeof(WellKnownMarker<ListValue>)),
        (BoolValue.Descriptor, typeof(WellKnownMarker<BoolValue>)),
        (BytesValue.Descriptor, typeof(WellKnownMarker<BytesValue>)),
        (StringValue.Descriptor, typeof(WellKnownMarker<StringValue>)),
        (Int32Value.Descriptor, typeof(WellKnownMarker<Int32Value>)),
        (Int64Value.Descriptor, typeof(WellKnownMarker<Int64Value>)),
        (UInt32Value.Descriptor, typeof(WellKnownMarker<UInt32Value>)),
        (UInt64Value.Descriptor, typeof(WellKnownMarker<UInt64Value>)),
        (FloatValue.Descriptor, typeof(WellKnownMarker<FloatValue>)),
        (DoubleValue.Descriptor, typeof(WellKnownMarker<DoubleValue>)),
    ];

    /// <summary>
    /// The messages of the well-known files that are ordinary messages: objects of expressions like any other.
    /// </summary>
    internal static readonly MessageDescriptor[] Plain =
    [
        Empty.Descriptor,
        FieldMask.Descriptor,
    ];

    /// <summary>
    /// Tells whether a message is one of those that CEL converts to a value of its own type, whose literal is that value.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns><see langword="true"/> for a converted message, found by reference.</returns>
    internal static bool IsConverted(MessageDescriptor message)
    {
        foreach ((MessageDescriptor descriptor, _) in Converted)
        {
            if (ReferenceEquals(descriptor, message))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Tells whether a file is one of the well-known files.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <returns><see langword="true"/> for a well-known file, found by reference.</returns>
    internal static bool Contains(FileDescriptor file)
    {
        foreach (FileDescriptor known in Files)
        {
            if (ReferenceEquals(known, file))
            {
                return true;
            }
        }

        return false;
    }
}

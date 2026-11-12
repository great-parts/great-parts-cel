using System;
using System.Collections;
using Google.Protobuf;

namespace Great.Parts.Cel.Protobuf;

/// <summary>
/// A registered proto2 extension: the typed object that <c>protoc</c> generates and the delegates, made by the generic method that
/// received it, that read and write it in a message without reflection.
/// </summary>
internal sealed class ExtensionField
{
    private readonly Func<object, object?> _get;
    private readonly Func<object, bool> _has;
    private readonly Action<object, object?>? _set;
    private readonly Func<object, IList>? _list;

    private ExtensionField(
        Extension registered,
        string extendedType,
        Func<object, object?> get,
        Func<object, bool> has,
        Action<object, object?>? set,
        Func<object, IList>? list)
    {
        Registered = registered;
        ExtendedType = extendedType;
        _get = get;
        _has = has;
        _set = set;
        _list = list;
    }

    /// <summary>
    /// The extension object as it was registered.
    /// </summary>
    internal Extension Registered { get; }

    /// <summary>
    /// The .NET name of the extended message class, for the text of an error.
    /// </summary>
    internal string ExtendedType { get; }

    /// <summary>
    /// Reads the value of the extension from a message: for a single extension its value, the default when it is not set; for a
    /// repeated extension its list, or <see langword="null"/> when it has none.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>The value, boxed.</returns>
    internal object? Get(object message) => _get(message);

    /// <summary>
    /// Tells whether a single extension is set in a message.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns><see langword="true"/> when the extension is set, also to its default.</returns>
    internal bool Has(object message) => _has(message);

    /// <summary>
    /// Sets a single extension in a message.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="value">The value, boxed, of the type of the extension.</param>
    internal void Set(object message, object? value) => _set!(message, value);

    /// <summary>
    /// Gives the list of a repeated extension in a message, which it creates when the message has none.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>The list to which elements are added.</returns>
    internal IList GetList(object message) => _list!(message);

    /// <summary>
    /// Makes the access to a single extension.
    /// </summary>
    /// <typeparam name="TMessage">The extended message.</typeparam>
    /// <typeparam name="TValue">The type of the value.</typeparam>
    /// <param name="extension">The extension object.</param>
    /// <returns>The access.</returns>
    internal static ExtensionField Create<TMessage, TValue>(Extension<TMessage, TValue> extension)
        where TMessage : IExtendableMessage<TMessage>
    {
        return new ExtensionField(
            extension,
            typeof(TMessage).FullName ?? typeof(TMessage).Name,
            message => ((IExtendableMessage<TMessage>)message).GetExtension(extension),
            message => ((IExtendableMessage<TMessage>)message).HasExtension(extension),
            (message, value) => ((IExtendableMessage<TMessage>)message).SetExtension(extension, (TValue)value!),
            null);
    }

    /// <summary>
    /// Makes the access to a repeated extension.
    /// </summary>
    /// <typeparam name="TMessage">The extended message.</typeparam>
    /// <typeparam name="TValue">The type of one element.</typeparam>
    /// <param name="extension">The extension object.</param>
    /// <returns>The access.</returns>
    internal static ExtensionField Create<TMessage, TValue>(RepeatedExtension<TMessage, TValue> extension)
        where TMessage : IExtendableMessage<TMessage>
    {
        return new ExtensionField(
            extension,
            typeof(TMessage).FullName ?? typeof(TMessage).Name,
            message => ((IExtendableMessage<TMessage>)message).GetExtension(extension),
            static _ => false,
            null,
            message => ((IExtendableMessage<TMessage>)message).GetOrInitializeExtension(extension));
    }
}

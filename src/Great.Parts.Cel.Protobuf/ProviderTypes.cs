using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace Great.Parts.Cel.Protobuf;

/// <summary>
/// What the descriptions of one provider share: the registry of the messages by their full names, which unpacking an <c>Any</c>
/// looks up, the description of each message by its descriptor, and the formatter that writes a message as JSON. The keys are the
/// names and the descriptors of the files, never text of an expression or data.
/// </summary>
internal sealed class ProviderTypes
{
    private readonly FrozenDictionary<string, MessageParser>.AlternateLookup<ReadOnlySpan<char>> _parsers;
    private readonly Dictionary<MessageDescriptor, MessageType> _types;
    private readonly FileDescriptor[] _files;
    private JsonFormatter? _json;

    /// <summary>
    /// Creates the registry of a provider.
    /// </summary>
    /// <param name="sourceName">The name of the source, which the text of an error names.</param>
    /// <param name="parsers">The parser of each message that an <c>Any</c> can hold, by full name.</param>
    /// <param name="types">The description of each message that the provider describes, by descriptor.</param>
    /// <param name="files">The files of the provider, for the type registry of the JSON formatter.</param>
    internal ProviderTypes(
        string sourceName,
        FrozenDictionary<string, MessageParser> parsers,
        Dictionary<MessageDescriptor, MessageType> types,
        FileDescriptor[] files)
    {
        SourceName = sourceName;
        _parsers = parsers.GetAlternateLookup<ReadOnlySpan<char>>();
        _types = types;
        _files = files;
    }

    /// <summary>
    /// The name of the source of the provider.
    /// </summary>
    internal string SourceName { get; }

    /// <summary>
    /// The formatter that writes a message in the canonical JSON form of protobuf, created on first use: only an expression that
    /// writes a message into a <c>Value</c> needs it.
    /// </summary>
    internal JsonFormatter Json
    {
        get
        {
            JsonFormatter? json = Volatile.Read(ref _json);
            if (json is null)
            {
                var created = new JsonFormatter(JsonFormatter.Settings.Default.WithTypeRegistry(TypeRegistry.FromFiles(_files)));
                json = Interlocked.CompareExchange(ref _json, created, null) ?? created;
            }

            return json;
        }
    }

    /// <summary>
    /// Finds the parser of a message by its full name, without making a string of the name.
    /// </summary>
    /// <param name="name">The full name.</param>
    /// <param name="parser">The parser, with the extension registry of the provider, when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the provider describes the message.</returns>
    internal bool TryGetParser(ReadOnlySpan<char> name, [NotNullWhen(true)] out MessageParser? parser) => _parsers.TryGetValue(name, out parser);

    /// <summary>
    /// Finds the description of a message.
    /// </summary>
    /// <param name="descriptor">The descriptor.</param>
    /// <param name="type">The description when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the provider describes the message.</returns>
    internal bool TryGetType(MessageDescriptor descriptor, [NotNullWhen(true)] out MessageType? type) => _types.TryGetValue(descriptor, out type);
}

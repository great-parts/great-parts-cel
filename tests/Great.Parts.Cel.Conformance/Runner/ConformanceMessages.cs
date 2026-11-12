using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Great.Parts.Cel.Protobuf;
using Great.Parts.Cel.Values;
using Any = Google.Protobuf.WellKnownTypes.Any;
using ExtensionsFile = Cel.Expr.Conformance.Proto2.TestAllTypesExtensionsReflection;
using Proto2File = Cel.Expr.Conformance.Proto2.TestAllTypesReflection;
using Proto3File = Cel.Expr.Conformance.Proto3.TestAllTypesReflection;
using ScopedExtensions = Cel.Expr.Conformance.Proto2.Proto2ExtensionScopedMessage.Extensions;
using TopLevelExtensions = Cel.Expr.Conformance.Proto2.TestAllTypesExtensionsExtensions;

namespace Great.Parts.Cel.Conformance.Runner;

/// <summary>
/// The test messages of the cases and the protobuf provider the runner builds from them (docs/design/conformance.md, "Messages,
/// <c>Any</c> and the provider of the cases"): the provider of the shared setup, the table of the messages for unpacking an
/// <c>Any</c>, and the extension registry that parses a message the way the provider reads it.
/// </summary>
internal static class ConformanceMessages
{
    private const string SourceName = "conformance";

    private static readonly FileDescriptor[] Files = [Proto2File.Descriptor, Proto3File.Descriptor, ExtensionsFile.Descriptor];

    private static readonly ExtensionRegistry Registry = CreateRegistry();

    // The messages of the three files and of the files they import, nested ones included, by full name.
    private static readonly Dictionary<string, MessageParser> Parsers = CreateParsers();

    /// <summary>The provider of every case: the three test files, their imports, the known types and the nine proto2 extensions.</summary>
    internal static CelProtobufTypes Provider { get; } = CreateProvider();

    // The descriptions of the provider by full name; a message found in the table is paired with its description.
    private static readonly Dictionary<string, CelObjectType> Descriptions = CreateDescriptions();

    /// <summary>
    /// Unpacks an <c>Any</c> of a message of the table and pairs the message with its description in the provider, so that the value
    /// carries its description wherever a case puts it. This is the resolver of <see cref="ValueConversion.MessageResolver"/>.
    /// </summary>
    /// <param name="any">The <c>Any</c>.</param>
    /// <returns>The object value, or <see langword="null"/> when the table or the provider lacks the message.</returns>
    internal static CelValue? Resolve(Any any) =>
        TryUnpack(any, out IMessage? message) && Descriptions.TryGetValue(message.Descriptor.FullName, out CelObjectType? description)
            ? CelValue.FromObject(description, message)
            : null;

    /// <summary>Parses the content of an <c>Any</c> with the extensions of the registry, when the table has the message.</summary>
    /// <param name="any">The <c>Any</c>: the name of the message is the text of its type URL after the last slash.</param>
    /// <param name="message">The message, or <see langword="null"/>.</param>
    internal static bool TryUnpack(Any any, [NotNullWhen(true)] out IMessage? message)
    {
        string url = any.TypeUrl;
        if (Parsers.TryGetValue(url[(url.AsSpan().LastIndexOf('/') + 1)..], out MessageParser? parser))
        {
            message = parser.ParseFrom(any.Value);
            return true;
        }

        message = null;
        return false;
    }

    /// <summary>
    /// Whether two <c>Any</c> values are equal: both unpacked with the table and compared by the <c>Equals</c> of the generated class,
    /// which takes maps in any order, covers the extensions of the registry and compares a floating-point field by its bits; an
    /// <c>Any</c> of a message the table lacks is compared by its type URL and its bytes.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    /// <param name="actual">The actual value.</param>
    internal static bool AreEqual(Any expected, Any actual) =>
        TryUnpack(expected, out IMessage? left) && TryUnpack(actual, out IMessage? right)
            ? left.Equals(right)
            : expected.Equals(actual);

    /// <summary>The text of an <c>Any</c> for the reason of a failed case: its type URL and, for a message of the table, the message.</summary>
    /// <param name="any">The <c>Any</c>.</param>
    internal static string Text(Any any) =>
        TryUnpack(any, out IMessage? message) ? "object " + any.TypeUrl + " " + message : "object " + any.TypeUrl;

    private static ExtensionRegistry CreateRegistry()
    {
        // The same nine objects as in CreateProvider: five of the top level of the file and four of Proto2ExtensionScopedMessage.
        ExtensionRegistry registry = new();
        registry.AddRange(
        [
            TopLevelExtensions.Int32Ext,
            TopLevelExtensions.NestedExt,
            TopLevelExtensions.TestAllTypesExt,
            TopLevelExtensions.NestedEnumExt,
            TopLevelExtensions.RepeatedTestAllTypes,
            ScopedExtensions.Int64Ext,
            ScopedExtensions.MessageScopedNestedExt,
            ScopedExtensions.NestedEnumExt,
            ScopedExtensions.MessageScopedRepeatedTestAllTypes,
        ]);
        return registry;
    }

    private static Dictionary<string, MessageParser> CreateParsers()
    {
        Dictionary<string, MessageParser> parsers = new(StringComparer.Ordinal);
        HashSet<FileDescriptor> seen = [];
        Stack<FileDescriptor> files = new(Files);
        Stack<MessageDescriptor> messages = new();
        while (files.Count > 0)
        {
            FileDescriptor file = files.Pop();
            if (!seen.Add(file))
            {
                continue;
            }

            foreach (FileDescriptor dependency in file.Dependencies)
            {
                files.Push(dependency);
            }

            foreach (MessageDescriptor message in file.MessageTypes)
            {
                messages.Push(message);
            }
        }

        while (messages.Count > 0)
        {
            MessageDescriptor message = messages.Pop();
            foreach (MessageDescriptor nested in message.NestedTypes)
            {
                messages.Push(nested);
            }

            // The entry message of a map field has no class, so no parser: it is no type of a case.
            if (message.Parser is { } parser)
            {
                parsers[message.FullName] = parser.WithExtensionRegistry(Registry);
            }
        }

        return parsers;
    }

    private static CelProtobufTypes CreateProvider()
    {
        CelProtobufTypesBuilder builder = new(SourceName);
        foreach (FileDescriptor file in Files)
        {
            builder.AddFile(file);
        }

        return builder
            .AddProtoExtension(TopLevelExtensions.Int32Ext)
            .AddProtoExtension(TopLevelExtensions.NestedExt)
            .AddProtoExtension(TopLevelExtensions.TestAllTypesExt)
            .AddProtoExtension(TopLevelExtensions.NestedEnumExt)
            .AddProtoExtension(TopLevelExtensions.RepeatedTestAllTypes)
            .AddProtoExtension(ScopedExtensions.Int64Ext)
            .AddProtoExtension(ScopedExtensions.MessageScopedNestedExt)
            .AddProtoExtension(ScopedExtensions.NestedEnumExt)
            .AddProtoExtension(ScopedExtensions.MessageScopedRepeatedTestAllTypes)
            .Build();
    }

    private static Dictionary<string, CelObjectType> CreateDescriptions()
    {
        Dictionary<string, CelObjectType> descriptions = new(StringComparer.Ordinal);
        foreach (CelObjectType type in Provider.ObjectTypes)
        {
            descriptions[type.Name] = type;
        }

        return descriptions;
    }
}

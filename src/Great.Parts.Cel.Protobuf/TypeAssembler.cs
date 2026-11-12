using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Google.Protobuf.WellKnownTypes;
using Great.Parts.Cel.Values;

namespace Great.Parts.Cel.Protobuf;

/// <summary>
/// Makes a provider from the walk over the files and the registered extensions: a new set of descriptions each time, in the
/// order of the full names, so that the order of the calls of the builder changes nothing.
/// </summary>
internal static class TypeAssembler
{
    /// <summary>
    /// Creates the provider.
    /// </summary>
    /// <param name="sourceName">The name of the source.</param>
    /// <param name="walker">The files added to the builder.</param>
    /// <param name="registered">The registered extensions.</param>
    /// <returns>The provider.</returns>
    /// <exception cref="InvalidOperationException">An extension is declared in a file that was not added, or a message would have two fields with one name.</exception>
    internal static CelProtobufTypes Build(string sourceName, FileWalker walker, List<ExtensionField> registered)
    {
        Dictionary<MessageDescriptor, List<BoundExtension>> extensions = BindExtensions(walker, registered);
        List<(MessageDescriptor Descriptor, System.Type ClrType)> messages = [];
        foreach (MessageDescriptor message in walker.Messages)
        {
            messages.Add((message, message.ClrType!));
        }

        foreach ((MessageDescriptor descriptor, System.Type marker) in WellKnownFiles.Converted)
        {
            messages.Add((descriptor, marker));
        }

        foreach (MessageDescriptor plain in WellKnownFiles.Plain)
        {
            messages.Add((plain, plain.ClrType!));
        }

        messages.Sort(static (left, right) => string.CompareOrdinal(left.Descriptor.FullName, right.Descriptor.FullName));

        var empties = new Dictionary<MessageDescriptor, IMessage>();
        var objectTypes = new CelObjectType[messages.Count];
        var byDescriptor = new Dictionary<MessageDescriptor, MessageType>(messages.Count);
        for (int index = 0; index < objectTypes.Length; index++)
        {
            (MessageDescriptor descriptor, System.Type clrType) = messages[index];
            extensions.TryGetValue(descriptor, out List<BoundExtension>? own);
            MessageType type = CreateType(descriptor, clrType, own, empties);
            objectTypes[index] = type;
            byDescriptor.TryAdd(descriptor, type);
        }

        ExtensionRegistry? registry = CreateExtensionRegistry(extensions);
        var shared = new ProviderTypes(sourceName, CreateParsers(messages, registry), byDescriptor, CreateFiles(walker));
        foreach (CelObjectType type in objectTypes)
        {
            ((MessageType)type).Attach(shared);
        }

        return new CelProtobufTypes(sourceName, objectTypes, CreateEnumTypes(walker));
    }

    // The registry of the registered extensions, in the order of their full names, with which the content of an Any is parsed;
    // null when no extension is registered.
    private static ExtensionRegistry? CreateExtensionRegistry(Dictionary<MessageDescriptor, List<BoundExtension>> extensions)
    {
        List<BoundExtension> all = [];
        foreach (List<BoundExtension> list in extensions.Values)
        {
            all.AddRange(list);
        }

        if (all.Count == 0)
        {
            return null;
        }

        all.Sort(static (left, right) => string.CompareOrdinal(left.Field.FullName, right.Field.FullName));
        var registry = new ExtensionRegistry();
        foreach (BoundExtension extension in all)
        {
            registry.Add(extension.Access.Registered);
        }

        return registry;
    }

    // The parser of each message that an Any can hold, by full name: the messages of the provider, which parse with the registry of the
    // extensions, and Timestamp and Duration, which have no description. A name that two files declare keeps the first.
    private static FrozenDictionary<string, MessageParser> CreateParsers(
        List<(MessageDescriptor Descriptor, System.Type ClrType)> messages,
        ExtensionRegistry? registry)
    {
        var parsers = new Dictionary<string, MessageParser>(StringComparer.Ordinal);
        foreach ((MessageDescriptor descriptor, _) in messages)
        {
            MessageParser parser = registry is null || WellKnownFiles.IsConverted(descriptor)
                ? descriptor.Parser
                : descriptor.Parser.WithExtensionRegistry(registry);
            parsers.TryAdd(descriptor.FullName, parser);
        }

        parsers.TryAdd(Timestamp.Descriptor.FullName, Timestamp.Parser);
        parsers.TryAdd(Duration.Descriptor.FullName, Duration.Parser);
        return parsers.ToFrozenDictionary(StringComparer.Ordinal);
    }

    // The files of the provider: those of the builder and the files of the known types.
    private static FileDescriptor[] CreateFiles(FileWalker walker)
    {
        var files = new List<FileDescriptor>(walker.Files);
        files.AddRange(WellKnownFiles.Files);
        return [.. files];
    }

    // The registered extensions by the message they extend, each list in the order of the full names of the extensions. An extension
    // whose declaring file or extended message is not among the files is an error, with all such extensions named in one exception.
    private static Dictionary<MessageDescriptor, List<BoundExtension>> BindExtensions(FileWalker walker, List<ExtensionField> registered)
    {
        var bound = new Dictionary<MessageDescriptor, List<BoundExtension>>();
        var missing = new List<string>();
        var taken = new HashSet<FieldDescriptor>();
        foreach (ExtensionField extension in registered)
        {
            FieldDescriptor? field = walker.FindExtension(extension.Registered);
            if (field is null || !walker.Describes(field.ExtendeeType))
            {
                missing.Add(
                    "the extension with the field number " + extension.Registered.FieldNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    + " of message " + extension.ExtendedType + " is declared in a file that was not added");
            }
            else if (taken.Add(field))
            {
                ref List<BoundExtension>? list = ref CollectionsMarshal.GetValueRefOrAddDefault(bound, field.ExtendeeType, out _);
                (list ??= []).Add(new BoundExtension(field, extension));
            }
        }

        if (missing.Count > 0)
        {
            missing.Sort(StringComparer.Ordinal);
            throw new InvalidOperationException(string.Join("; ", missing));
        }

        foreach (List<BoundExtension> list in bound.Values)
        {
            list.Sort(static (left, right) => string.CompareOrdinal(left.Field.FullName, right.Field.FullName));
        }

        return bound;
    }

    // One description of a message: its fields in the order of declaration, then its registered extensions; the number of a field is its
    // position. The plans need the description, so they are made after it and given to it before it is shared.
    private static MessageType CreateType(MessageDescriptor descriptor, System.Type clrType, List<BoundExtension>? extensions, Dictionary<MessageDescriptor, IMessage> empties)
    {
        IList<FieldDescriptor> declared = descriptor.Fields.InDeclarationOrder();
        int count = declared.Count + (extensions?.Count ?? 0);
        var fields = new FieldDescriptor[count];
        var shapes = new FieldShape[count];
        var celFields = new CelObjectField[count];
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (int number = 0; number < count; number++)
        {
            bool isExtension = number >= declared.Count;
            fields[number] = isExtension ? extensions![number - declared.Count].Field : declared[number];
            string name = isExtension ? fields[number].FullName : fields[number].Name;
            if (!names.Add(name))
            {
                throw new InvalidOperationException("the message " + descriptor.FullName + " would have two fields named '" + name + "'");
            }

            shapes[number] = FieldShape.Of(fields[number]);
            celFields[number] = new CelObjectField(number, name, shapes[number].CelType);
        }

        var type = new MessageType(descriptor, clrType, celFields, WellKnownFiles.IsConverted(descriptor));
        var plans = new FieldPlan[count];
        for (int number = 0; number < count; number++)
        {
            ExtensionField? extension = number >= declared.Count ? extensions![number - declared.Count].Access : null;
            plans[number] = FieldPlan.Create(type, number, fields[number], extension, shapes[number], EmptyOf(shapes[number].Value.Message, empties));
        }

        type.Initialize(plans);
        return type;
    }

    // The empty message that an unset message field reads as, one for each message of the provider, shared and never changed.
    private static IMessage? EmptyOf(MessageDescriptor? message, Dictionary<MessageDescriptor, IMessage> empties)
    {
        if (message is null)
        {
            return null;
        }

        ref IMessage? empty = ref CollectionsMarshal.GetValueRefOrAddDefault(empties, message, out bool exists);
        if (!exists)
        {
            empty = message.Parser.ParseFrom(ReadOnlySpan<byte>.Empty);
        }

        return empty;
    }

    // Every enumeration of the files and of the well-known files, in the order of the full names, with the constants by name and number.
    private static CelEnumType[] CreateEnumTypes(FileWalker walker)
    {
        List<EnumDescriptor> enums = [.. walker.Enums];
        foreach (FileDescriptor file in WellKnownFiles.Files)
        {
            enums.AddRange(file.EnumTypes);
        }

        enums.Sort(static (left, right) => string.CompareOrdinal(left.FullName, right.FullName));
        var types = new CelEnumType[enums.Count];
        for (int index = 0; index < types.Length; index++)
        {
            IList<EnumValueDescriptor> values = enums[index].Values;
            var constants = new KeyValuePair<string, long>[values.Count];
            for (int value = 0; value < constants.Length; value++)
            {
                constants[value] = new KeyValuePair<string, long>(values[value].Name, values[value].Number);
            }

            types[index] = new CelEnumType(enums[index].FullName, constants);
        }

        return types;
    }

    // An extension with the field that declares it.
    private readonly record struct BoundExtension(FieldDescriptor Field, ExtensionField Access);
}

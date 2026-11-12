using System;
using System.Collections.Generic;
using Google.Protobuf;
using Google.Protobuf.Reflection;

namespace Great.Parts.Cel.Protobuf;

/// <summary>
/// The walk over the descriptors of the files of a builder: the files and the files they import, the messages with the
/// messages nested in them, the enumerations and the extension fields. The walk is iterative, with stacks of its own, so
/// that no depth of nesting reaches the thread stack.
/// </summary>
internal sealed class FileWalker
{
    private readonly HashSet<FileDescriptor> _files = [];
    private readonly HashSet<MessageDescriptor> _messageSet = [];

    /// <summary>
    /// The messages of the files, in the order of the walk, without the messages of the well-known files and without the
    /// entry messages of map fields, which are no type of CEL.
    /// </summary>
    internal List<MessageDescriptor> Messages { get; } = [];

    /// <summary>
    /// The enumerations of the files, those nested in messages included.
    /// </summary>
    internal List<EnumDescriptor> Enums { get; } = [];

    /// <summary>
    /// The extension fields declared in the files, at the top level and in messages.
    /// </summary>
    internal List<FieldDescriptor> Extensions { get; } = [];

    /// <summary>
    /// The files that were added and the files they import, without the well-known files.
    /// </summary>
    internal IReadOnlyCollection<FileDescriptor> Files => _files;

    /// <summary>
    /// Adds a file and the files it imports, each once; a file that was added before and the well-known files change nothing.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <exception cref="ArgumentException">A message of the files has no .NET class.</exception>
    internal void Add(FileDescriptor file)
    {
        List<FileDescriptor> reached = ReachNewFiles(file);
        var messages = new List<MessageDescriptor>();
        var enums = new List<EnumDescriptor>();
        var extensions = new List<FieldDescriptor>();
        foreach (FileDescriptor reachedFile in reached)
        {
            Collect(reachedFile, messages, enums, extensions);
        }

        foreach (FileDescriptor reachedFile in reached)
        {
            _files.Add(reachedFile);
        }

        foreach (MessageDescriptor message in messages)
        {
            _messageSet.Add(message);
        }

        Messages.AddRange(messages);
        Enums.AddRange(enums);
        Extensions.AddRange(extensions);
    }

    /// <summary>
    /// Finds the descriptor of an extension object that was registered.
    /// </summary>
    /// <param name="extension">The extension object.</param>
    /// <returns>The field descriptor whose extension is that object, or <see langword="null"/> when no file of the walk declares it.</returns>
    internal FieldDescriptor? FindExtension(Extension extension)
    {
        foreach (FieldDescriptor field in Extensions)
        {
            if (ReferenceEquals(field.Extension, extension))
            {
                return field;
            }
        }

        return null;
    }

    /// <summary>
    /// Tells whether a message is one of the messages of the files of the walk.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns><see langword="true"/> when the message was reached.</returns>
    internal bool Describes(MessageDescriptor message) => _messageSet.Contains(message);

    // The files that the walk has not seen yet, reachable from this file by imports.
    private List<FileDescriptor> ReachNewFiles(FileDescriptor file)
    {
        var reached = new List<FileDescriptor>();
        var seen = new HashSet<FileDescriptor>();
        var pending = new Stack<FileDescriptor>();
        pending.Push(file);
        while (pending.Count > 0)
        {
            FileDescriptor current = pending.Pop();
            if (WellKnownFiles.Contains(current) || _files.Contains(current) || !seen.Add(current))
            {
                continue;
            }

            reached.Add(current);
            foreach (FileDescriptor dependency in current.Dependencies)
            {
                pending.Push(dependency);
            }
        }

        return reached;
    }

    private static void Collect(FileDescriptor file, List<MessageDescriptor> messages, List<EnumDescriptor> enums, List<FieldDescriptor> extensions)
    {
        enums.AddRange(file.EnumTypes);
        extensions.AddRange(file.Extensions.UnorderedExtensions);
        var pending = new Stack<MessageDescriptor>();
        foreach (MessageDescriptor message in file.MessageTypes)
        {
            pending.Push(message);
        }

        while (pending.Count > 0)
        {
            MessageDescriptor current = pending.Pop();
            if (current.IsMapEntry)
            {
                continue;
            }

            if (current.ClrType is null)
            {
                throw new ArgumentException(
                    "The file '" + file.Name + "' was not generated by protoc for C#: the message '" + current.FullName + "' has no .NET class, so its fields cannot be read.",
                    nameof(file));
            }

            messages.Add(current);
            enums.AddRange(current.EnumTypes);
            extensions.AddRange(current.Extensions.UnorderedExtensions);
            foreach (MessageDescriptor nested in current.NestedTypes)
            {
                pending.Push(nested);
            }
        }
    }
}
